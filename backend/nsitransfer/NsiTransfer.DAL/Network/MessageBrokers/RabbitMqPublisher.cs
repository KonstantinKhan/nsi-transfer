using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Exceptions;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Interfaces.MessageBrokers;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace NsiTransfer.DAL.Network.MessageBrokers;

/// <summary>
/// DAL слой для публикации сообщений в RabbitMQ
/// Отвечает за низкоуровневую работу с подключением и отправкой сообщений
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly ILogger<RabbitMqPublisher> _logger;

    private readonly Interfaces.MessageBrokers.IConnectionFactory _factory;
    private IConnection? _connection;
    private IModel? _channel;

    private readonly SemaphoreSlim _connectionLock = new (1, 1);
    private int _disposed = 0;
    private bool _connectionBlocked = false;

    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<ConfirmStatus>> _pendingSendings = new();
    private readonly ConcurrentDictionary<string, ulong> _messageIdToDeliveryTagOfPendingSendings = new();
    private readonly HashSet<string> _queuesDeclared = new();
    private readonly HashSet<string> _exchangesDeclared = new();
    private readonly HashSet<string> _bindingsDeclared = new();

    private readonly int _maxRetryAttempts;
    private readonly int _retryIntervalInSeconds;
    private readonly int _confirmationTimeOutInSeconds;
    
    
    private enum ConfirmStatus { Unknown = 0, Ack = 1, Nack = 2, BasicReturn = 3, TimedOut = 4, ChannelClosed = 5 }
    private record ConfirmResult(ConfirmStatus Status, Exception? Ex);
    private bool ConnectionAndChannelOpened => _connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen;
    
    private bool IsChannelOpened => _channel is { IsOpen: true };
    private bool IsConnectionOpened => _connection is { IsOpen: true };


    public bool IsConnectionBlocked => _connectionBlocked;

    public RabbitMqPublisher(
        Interfaces.MessageBrokers.IConnectionFactory connectionFactory, 
        ILogger<RabbitMqPublisher> logger,
        IOptions<RabbitMqRetryParams> retryParams)
    {
        _factory = connectionFactory;
        _logger = logger;
        
        _maxRetryAttempts = retryParams.Value.MaxRetryAttempts;
        _retryIntervalInSeconds = retryParams.Value.RetryIntervalInSeconds;
        _confirmationTimeOutInSeconds = retryParams.Value.ConfirmationTimeOutInSeconds;
    }


    /// <summary>
    /// Опубликовать сообщение в указанную очередь
    /// <exception cref="OperationCanceledException">Вызывающему методу необходимо отлавилвать <see cref="OperationCanceledException"/> самостоятельно</exception>
    /// <exception cref="AggregateException">Необходимо отлавливать <see cref="AggregateException"/>, в нём будет храниться информация о неудачных попытках подключения (для этого класс <see cref="RabbitMqConnectionAttemptFailedException"/>) к RabbitMQ или отправки сообщений (<see cref="RabbitMqPublishingAttemptFailedException"/>)</exception>
    /// <exception cref="ObjectDisposedException">Может быть выброшено, если RabbitMqPublisher будет высвобождать ресурсы с имеющимися в ожидании подтверждения отправками</exception>
    /// </summary>
    public async Task<RabbitMqPublishingResultEnum> PublishAsync(
        string exchangeName,
        string queueName, 
        string contentType, 
        string messageId, 
        string message, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(exchangeName, nameof(exchangeName));
        ArgumentException.ThrowIfNullOrEmpty(queueName, nameof(queueName));
        ArgumentException.ThrowIfNullOrEmpty(messageId, nameof(messageId));
        ArgumentException.ThrowIfNullOrEmpty(message, nameof(message));

        if (_connectionBlocked) return RabbitMqPublishingResultEnum.ConnectionBlocked;
        
        var publishingExceptions = new List<Exception>();

        for (int attempt = 0; attempt < _maxRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var delay = TimeSpan.FromSeconds((attempt + 1) * _retryIntervalInSeconds);

            await EnsureConnectionAsync(cancellationToken);

            ulong deliveryTag = 0;
            TaskCompletionSource<ConfirmStatus> tcs = null;


            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsChannelOpened)
                {
                    LogAndCollectException(publishingExceptions, attempt, delay, $"Открытый канал закрылся до начала публикации сообщения с MessageId '{messageId}' в брокер. Попытка переподключения.");
                    continue;
                }

                EnsureExchangeDeclared(exchangeName);
                EnsureQueueDeclared(queueName);
                EnsureQueueBound(exchangeName, queueName, routingKey: queueName);

                var properties = _channel.CreateBasicProperties();
                properties.ContentType = contentType ?? "text/plain";
                properties.DeliveryMode = 2; // Сохранение сообщений на диске, чтобы не потерять их при перезапуске RabbitMQ
                properties.MessageId = messageId;
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                deliveryTag = _channel.NextPublishSeqNo;
                tcs = new TaskCompletionSource<ConfirmStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingSendings.TryAdd(deliveryTag, tcs);
                _messageIdToDeliveryTagOfPendingSendings.TryAdd(messageId, deliveryTag);

                var body = Encoding.UTF8.GetBytes(message);

                cancellationToken.ThrowIfCancellationRequested();

                _channel.BasicPublish(
                    exchange: exchangeName,
                    routingKey: queueName,
                    mandatory: true,
                    basicProperties: properties,
                    body: body);
            }
            catch (AlreadyClosedException)
            {
                // может произойти в BasicPublish, если в этот момент обнаружится, что канал закрыт
                LogAndCollectException(publishingExceptions, attempt, delay, $"Канал закрылся в момент отправки сообщения с MessageId '{messageId}' в брокер. Попытка переподключения.");
                if (tcs != null) _pendingSendings.TryRemove(deliveryTag, out _);
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (Exception)
            {
                LogAndCollectException(publishingExceptions, attempt, delay, $"Непредвиденная ошибка при отправке сообщения с MessageId '{messageId}' в брокер. Попытка переподключения.");
                if (tcs != null) _pendingSendings.TryRemove(deliveryTag, out _);
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            finally
            {
                _connectionLock.Release();
            }
            
            var confirm = await WaitForConfirmAsync(tcs, deliveryTag, messageId, cancellationToken);

            switch (confirm.Status)
            {
                case ConfirmStatus.Ack:
                    _logger.LogInformation("Сообщение с MessageId '{MessageId}' было подтверждено брокером.", messageId);
                    return RabbitMqPublishingResultEnum.Ack;

                case ConfirmStatus.Nack:
                    _logger.LogError("Сообщение с MessageId '{MessageId}' отклонено брокером.", messageId);
                    return RabbitMqPublishingResultEnum.Nack;

                case ConfirmStatus.TimedOut:
                    _logger.LogError("В течение {ConfirmationTimeOut} сек. не было получено подтверждение на сообщение с MessageId '{MessageId}' от брокера.", _confirmationTimeOutInSeconds, messageId);
                    return RabbitMqPublishingResultEnum.TimedOut;

                case ConfirmStatus.ChannelClosed:
                    LogAndCollectException(publishingExceptions, attempt, delay, $"Во время ожидания подтверждения от брокера о получении сообщения с MessageId '{messageId}' канал связи был обнаружен закрытым. Попытка переподключения.");
                    await Task.Delay(delay, cancellationToken);
                    break;

                case ConfirmStatus.BasicReturn:
                    LogAndCollectException(publishingExceptions, attempt, delay, $"Сообщение с MessageId '{messageId}' не удалось маршрутизировать в нужную очередь в брокере (BasicReturn raised). Попытка отправить сообщение вновь.");
                    await Task.Delay(delay, cancellationToken);
                    break;

                case ConfirmStatus.Unknown:
                    _logger.LogError(confirm.Ex, "Неизвестная ошибка в процессе ожидания подтверждения от брокера о полечении сообщения с MessageId '{MessageId}'.", messageId);
                    return RabbitMqPublishingResultEnum.Unknown;
            }
        }


        _logger.LogError("Сообщение с MessageId '{MessageId}' не опубликовано после {Attempts} попыток.", messageId, _maxRetryAttempts);
        throw new AggregateException($"Сообщение с MessageId '{messageId}' не опубликовано после {_maxRetryAttempts} попыток.", publishingExceptions);
    }

    private async Task<ConfirmResult> WaitForConfirmAsync(TaskCompletionSource<ConfirmStatus> tcs, ulong deliveryTag, string messageId, CancellationToken externalCt)
    {
        // делаем новый CancellationToken, который отработает либо сам по таймеру, либо по событию внешнего токена
        using var pairedCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        pairedCts.CancelAfter(TimeSpan.FromSeconds(_confirmationTimeOutInSeconds));

        try
        {
            var confirmStatus = await tcs.Task.WaitAsync(pairedCts.Token);
            return new ConfirmResult(confirmStatus, null);
        }
        catch (OperationCanceledException) when (!externalCt.IsCancellationRequested)
        {
            _pendingSendings.TryRemove(deliveryTag, out var _);

            // Значит произошёл time out по ожиданию ответа от RabbitMQ
            return IsChannelOpened ? new ConfirmResult(ConfirmStatus.TimedOut, null) : new ConfirmResult(ConfirmStatus.ChannelClosed, null);
        }
        catch (OperationCanceledException)
        {
            _pendingSendings.TryRemove(deliveryTag, out _);
            throw;
        }
        catch (AlreadyClosedException)
        {
            // OnModelShutdown выставил TrySetException(AllreadyClosedException)
            // _pendingSendings уже почищен в OnModelShutdown
            return new ConfirmResult(ConfirmStatus.ChannelClosed, null);
        }
        catch (ObjectDisposedException)
        {
            // В DisposeAsync выставлено TrySetException(ObjectDisposedException), это ненормальное поведение, нужно выбросить Exception
            throw;
        }
        catch (Exception ex)
        {
            return new ConfirmResult(ConfirmStatus.Unknown, ex);
        }
    }



    private void EnsureExchangeDeclared(string exchangeName)
    {
        if (string.IsNullOrEmpty(exchangeName)) return;

        if (_exchangesDeclared.Contains(exchangeName)) return;

        _channel.ExchangeDeclare(
                        exchange: exchangeName,
                        type: "direct",
                        durable: true,
                        autoDelete: false,
                        arguments: null);

        _logger.LogDebug("Exchange '{ExchangeName}' объявлен", exchangeName);
        _exchangesDeclared.Add(exchangeName);
    }

    private void EnsureQueueDeclared(string queueName)
    {
        if (_queuesDeclared.TryGetValue(queueName, out _)) return; // значит очередь уже определена

        _channel.QueueDeclare(
                        queue: queueName,
                        durable: true, // Сохраниение очереди после перезапуска RabbitMQ
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

        _logger.LogDebug("Очередь '{QueueName}' объявлена", queueName);
    }

    private void EnsureQueueBound(string exchangeName, string queueName, string routingKey)
    {
        // Формируем уникальный ключ привязки, чтобы не спамить брокер повторными bind
        string bindKey = $"{exchangeName}:{queueName}:{routingKey}";
        if (_bindingsDeclared.Contains(bindKey)) return;

        _channel.QueueBind(
                        queue: queueName,
                        exchange: exchangeName,
                        routingKey: routingKey,
                        arguments: null);

        _logger.LogDebug("Очередь '{QueueName}' привязана к обменнику '{ExchangeName}' с ключом '{RoutingKey}'", queueName, exchangeName, routingKey);

        _bindingsDeclared.Add(bindKey);
    }


    private void LogAndCollectException(List<Exception> list, int attempt, TimeSpan delay, string errorMessage, Exception? innerException = null)
    {
        var fullError = $"Попытка {attempt + 1} из {_maxRetryAttempts}, следующая через {delay.TotalSeconds}сек. \n{errorMessage}";
        _logger.LogWarning(fullError);
        list.Add(innerException == null
                    ? new RabbitMqPublishingAttemptFailedException(fullError)
                    : new RabbitMqPublishingAttemptFailedException(fullError, innerException));
    }










    /// <summary>
    /// Убедиться, что соединение с RabbitMQ установлено
    /// </summary>
    private async Task EnsureConnectionAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (ConnectionAndChannelOpened) return;

            await CreateConnectionAndChannelWithRetry(cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task CreateConnectionAndChannelWithRetry(CancellationToken cancellationToken = default)
    {
        _queuesDeclared.Clear();
        _exchangesDeclared.Clear();
        _bindingsDeclared.Clear();

        var collectedExceptions = new List<Exception>(_maxRetryAttempts);

        for (int retryCount = 0; retryCount < _maxRetryAttempts; retryCount++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            IConnection? currentConnection = null;
            IModel? currentChannel = null;
            var currentDelay = TimeSpan.FromSeconds(_retryIntervalInSeconds * (retryCount + 1));

            try
            {
                currentConnection = _factory.CreateConnection();
                currentChannel = currentConnection.CreateModel();

                currentChannel.ConfirmSelect(); // Включаем режим подтверждения сообщений от RabbitMQ

                SubscribeConnectionEvents(currentConnection);
                SubscribeChannelEvents(currentChannel);

                _connection = currentConnection;
                _channel = currentChannel;
                

                _logger.LogInformation(
                    "Соединение с RabbitMQ установлено. API: {ApiName}, Protocol: {MajorVersion}.{MinorVersion}",
                    _connection.Protocol.ApiName,
                    _connection.Protocol.MajorVersion,
                    _connection.Protocol.MinorVersion);

                return;
            }
            catch (BrokerUnreachableException ex) when (ex.InnerException is AuthenticationFailureException)
            {
                DisposeResources(currentConnection, currentChannel);
                collectedExceptions.Add(ex);
                throw new AggregateException("Обнаружена ошибка аутентификации в RabbitMQ. Проверьте правильность данных учётной записи.", collectedExceptions);
            }
            catch (BrokerUnreachableException ex)
            {
                var errorMessage = $"Попытка {retryCount + 1} из {_maxRetryAttempts}, следующая через {currentDelay}сек. " +
                                   $"\nException name: {nameof(BrokerUnreachableException)} " +
                                   $"\nОписание: {ex.Message} | {ResolveSocketErrorMessage(ex)} | {ex.InnerException?.Message}";
                
                _logger.LogWarning(errorMessage);
                collectedExceptions.Add(new RabbitMqConnectionAttemptFailedException(errorMessage, ex));
                DisposeResources(currentConnection, currentChannel);
                await Task.Delay(currentDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Попытка {retryCount + 1} из {_maxRetryAttempts}, следующая через {currentDelay}сек. " +
                                   $"\nException name: {nameof(Exception)} " +
                                   $"\nОписание: {ex.Message}";
                
                _logger.LogWarning(errorMessage);
                collectedExceptions.Add(new RabbitMqConnectionAttemptFailedException(errorMessage, ex));
                DisposeResources(currentConnection, currentChannel);
                await Task.Delay(currentDelay, cancellationToken);
            }
        }
        
        _logger.LogError("Не удалось подключиться к RabbitMQ после {MaxRetryAttempts} попыток.", _maxRetryAttempts);
        throw new AggregateException($"Не удалось подключиться к RabbitMQ после {_maxRetryAttempts} попыток.", collectedExceptions);
        
        
        
        
        
        static string ResolveSocketErrorMessage(BrokerUnreachableException ex)
        {
            if (ex.InnerException is not SocketException se) return string.Empty;
            
            var socketError = se.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => "Порт закрыт - RabbitMQ не запущен",
                SocketError.HostNotFound => "DNS не резолвится - неверный хост",
                SocketError.TimedOut => "Хост есть, но не отвечает - файрвол?",
                SocketError.NetworkUnreachable => "Сеть недоступна",
                _ => $"Сетевая ошибка: {se.SocketErrorCode.ToString()}"
            };
            
            return socketError;
        }
        
        static void DisposeResources(IConnection? connection, IModel? channel)
        {
            // Close() может бросить, если соединение уже упало — подавляем
            try { channel?.Close();      } catch { /* ignored */ }
            try { channel?.Dispose();    } catch { /* ignored */ }
            try { connection?.Close();   } catch { /* ignored */ }
            try { connection?.Dispose(); } catch { /* ignored */ }
        }
    }

    private void SubscribeConnectionEvents(IConnection connection)
    {
        connection.ConnectionShutdown += OnConnectionShutdown;
        connection.CallbackException += OnCallbackException;
        connection.ConnectionBlocked += OnConnectionBlocked;
        connection.ConnectionUnblocked += OnConnectionUnblocked;
    }

    private void SubscribeChannelEvents(IModel channel)
    {
        channel.BasicAcks += OnBasicAcks;
        channel.BasicNacks += OnBasicNacks;
        channel.BasicReturn += OnBasicReturn;
        channel.ModelShutdown += OnModelShutdown;
    }


    /// <summary>
    /// Heartbeat timeout, обрыв сети, перезапуск брокера и другие события могут стать причиной возникновение ConnectionShutdown
    /// </summary>
    private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        if (e.Initiator == ShutdownInitiator.Application) return; // значит закрытие вызвало наше приложение, значит это норма

        _logger.LogError("Соединение RabbitMQ разорвано: {argsToString}", e.ToString());
        // Кажется, что лучше оставить ответственность за переподключение к брокеру на PublishAsync.
        // Тогда получится, что переподключаться back-end будет только перед запросом передачи сообщения в брокер.
    }

    /// <summary>
    /// Вызывается, когда появилось исключение в одном из обработчиков событий.
    /// Без подписки на это событие будут исключения пропадать молча.
    /// </summary>
    private void OnCallbackException(object? sender, RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Необработанное исключение в обработчиках событий RabbitMQ");
    }


    /// <summary>
    /// Брокер включил flow-control (memory alarm / disk alarm)
    /// Публикация будет блокироваться до снятия блокировки
    /// </summary>
    private void OnConnectionBlocked(object? sender, RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
    {
        _connectionBlocked = true;
        _logger.LogError("RabbitMQ заблокировал соединение по причине '{Reason}'. Публикация остановлена до снятия блокировки.", e.Reason);
    }

    /// <summary>
    /// Брокер снял блокировку соединения
    /// </summary>
    private void OnConnectionUnblocked(object? sender, EventArgs e)
    {
        _connectionBlocked = false;
        _logger.LogInformation("RabbitMQ снял блокировку соединения. Публикация возобновлена.");
    }



    private void OnBasicAcks(object? sender, RabbitMQ.Client.Events.BasicAckEventArgs e) => CompleteConfirms(e.DeliveryTag, e.Multiple, ConfirmStatus.Ack);

    private void OnBasicNacks(object? sender, RabbitMQ.Client.Events.BasicNackEventArgs e) => CompleteConfirms(e.DeliveryTag, e.Multiple, ConfirmStatus.Nack);

    private void OnBasicReturn(object? sender, RabbitMQ.Client.Events.BasicReturnEventArgs e)
    {
        _logger.LogError("Сообщение с MessageId '{MessageId}' не маршрутизировано в очередь. " +
            "\n[{Code}] {Text}. Exchange: {Exchange}, RoutingKey: {Key}",
            e.BasicProperties.MessageId, e.ReplyCode, e.ReplyText, e.Exchange, e.RoutingKey);

        if (_messageIdToDeliveryTagOfPendingSendings.TryRemove(e.BasicProperties.MessageId, out var deliveryTag) 
            && _pendingSendings.TryRemove(deliveryTag, out var tcs))
        {
            tcs.TrySetResult(ConfirmStatus.BasicReturn);
        }
    }

    private void OnModelShutdown(object? sender, ShutdownEventArgs e)
    {
        _logger.LogError("Канал RabbitMQ закрылся. Описание: {Desc}", e.ToString());

        var closedEx = new AlreadyClosedException(e);
        foreach (var kvp in _pendingSendings)
        {
            kvp.Value.TrySetException(closedEx);
        }

        _pendingSendings.Clear();
    }


    private void CompleteConfirms(ulong deliveryTag, bool multiple, ConfirmStatus status)
    {
        if (multiple)
        {
            foreach (var kvp in _pendingSendings.Where(kvp => kvp.Key <= deliveryTag))
            {
                if (_pendingSendings.TryRemove(deliveryTag, out var tcs))
                {
                    tcs.TrySetResult(status);
                }
            }
        }
        else
        {
            if (_pendingSendings.TryRemove(deliveryTag, out var tcs))
            {
                tcs.TrySetResult(status);
            }
        }
    }





    public async ValueTask DisposeAsync()
    {
        // если _disposed равно 0, то заменить на 1
        // если _disposed изначально (возврат из этой функции) был отличен от 0, то значит уже объект disposed, значит дальше не пытаемся его dispose'ить
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        var objDisposedEx = new ObjectDisposedException(nameof(RabbitMqPublisher));
        foreach (var kvp in _pendingSendings)
        {
            kvp.Value.TrySetException(objDisposedEx);
        }

        _pendingSendings.Clear();


        UnsubscribeChannelEvents(_channel);
        UnsubscribeConnectionEvents(_connection);


        if (_channel != null)
        {
            try { _channel.Close(); } catch { /* ignored */ }
            await CastAndDispose(_channel);
        }
        if (_connection != null)
        {
            try { _connection.Close(); } catch { /* ignored */ }
            await CastAndDispose(_connection);
        }
        await CastAndDispose(_connectionLock);

        _disposed = 1;

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }

    private void UnsubscribeConnectionEvents(IConnection? connection)
    {
        if (connection == null) return;

        connection.ConnectionShutdown -= OnConnectionShutdown;
        connection.CallbackException -= OnCallbackException;
        connection.ConnectionBlocked -= OnConnectionBlocked;
        connection.ConnectionUnblocked -= OnConnectionUnblocked;
    }

    private void UnsubscribeChannelEvents(IModel? channel)
    {
        if (channel == null) return;

        channel.BasicAcks -= OnBasicAcks;
        channel.BasicNacks -= OnBasicNacks;
        channel.BasicReturn -= OnBasicReturn;
        channel.ModelShutdown -= OnModelShutdown;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 1) throw new ObjectDisposedException(nameof(RabbitMqPublisher));
    }
}
