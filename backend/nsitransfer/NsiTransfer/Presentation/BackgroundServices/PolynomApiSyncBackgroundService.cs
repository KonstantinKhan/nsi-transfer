using Microsoft.Extensions.Options;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.Contract.ConfigModels;
using System.Diagnostics;
using System.Threading.Channels;

namespace NsiTransfer.Presentation.BackgroundServices;

public sealed class PolynomApiSyncBackgroundService : BackgroundService
{
    private readonly IOptionsHelper<AppConfiguration> _optionsHelper;
    private readonly IOptionsMonitor<PolynomApiSyncOptions> _apiSyncOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskQueue _emailTaskQueue;
    private readonly ILogger<PolynomApiSyncBackgroundService> _logger;

    private readonly Channel<bool> _configChangedChannel;
    private readonly IDisposable _onChangeRegistration;

    private DateTime _lastRunTime = DateTime.MinValue;
#if DEBUG
    private bool _firstStart = true;
#endif

    public PolynomApiSyncBackgroundService(
        IOptionsHelper<AppConfiguration> optionsHelper,
        IOptionsMonitor<PolynomApiSyncOptions> apiSyncOptions,
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskQueue emailTaskQueue,
        ILogger<PolynomApiSyncBackgroundService> logger)
    {
        _optionsHelper = optionsHelper;
        _apiSyncOptions = apiSyncOptions;
        _scopeFactory = scopeFactory;
        _emailTaskQueue = emailTaskQueue;
        _logger = logger;

        _configChangedChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false // OnChange теоретически может прилететь не из одного потока
        });

        // Подписываемся на изменения файла конфигурации.
        // Каждое срабатывание (в т.ч. ложные повторные от FileSystemWatcher) — пишем
        // в канал актуальный результат валидации, а не сам факт изменения.
        _onChangeRegistration = _optionsHelper.OnChange((updated, isValid) =>
        {
            var isReady = isValid && IsConfigurationValid(updated);
            _configChangedChannel.Writer.TryWrite(isReady);
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#if DEBUG
        if (_firstStart)
        {
            await Task.Delay(10000, stoppingToken);
            _firstStart = false;
        }
#endif

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan elapsed = TimeSpan.Zero;
            bool shouldDelay = false;

            try
            {
                if (IsCurrentConfigurationReady())
                {
                    elapsed = await RunWorkAsync(stoppingToken);
                    shouldDelay = true;
                }
                else
                {
                    _logger.LogInformation("Корректно заполненная конфигурация не найдена. Ожидание изменения файла конфигурации...");

                    // Ждём сигнала без поллинга — блокируемся на канале
                    var becameValid = await WaitForValidConfigAsync(stoppingToken);
                    if (becameValid)
                    {
                        _logger.LogInformation("Получено уведомление об изменении конфигурации. Повторная проверка...");

                        // Перепроверяем актуальное значение — на случай гонки или повторного изменения файла
                        if (IsCurrentConfigurationReady())
                        {
                            _logger.LogInformation("Корректная конфигурация обнаружена. {name} начал синхронизацию", nameof(PolynomApiSyncBackgroundService));

                            elapsed = await RunWorkAsync(stoppingToken);
                        }
                        else
                        {
                            var err = "Корректная конфигурация не найдена. " +
                                "Вероятно содержимое файла конфигурации было изменено повторно во время проверки, либо снова содержит не корректные значения. " +
                                $"{nameof(PolynomApiSyncBackgroundService)} отложил попытку синхронизации...";
                            _logger.LogWarning(err);
                            await NotifyAboutError(err, "Ошибка работы фонового сервиса синхронизации");
                        }

                        shouldDelay = true;
                    }
                }

                if (shouldDelay)
                {
                    var time = _apiSyncOptions.CurrentValue.StartSyncWithIntervalMinutes;
                    var interval = TimeSpan.FromMinutes(Math.Max(1, time));

                    var remaningDelay = interval - elapsed;
                    if (remaningDelay > TimeSpan.Zero)
                    {
                        await Task.Delay(remaningDelay, stoppingToken);
                    }
                    else
                    {
                        await Task.Yield(); // Если синхронизация заняла больше времени, чем интервал, то сразу же запустить следующую итерацию без задержки
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Фоновый сервис синхронизации объектов из Полинома с внешней системой остановлена по запросу отмены.");
                break;
            }
            catch (Exception ex)
            {
                var err = "Фоновый сервис синхронизации объектов из Полинома с внешней системой прервался из-за возникновения неожиданного исключения.";
                _logger.LogError(ex, err);
                await NotifyAboutError(err, ex, err);
            }
        }
    }

    private bool IsCurrentConfigurationReady()
    {
        return _optionsHelper.TryGetCurrentValue(out var config) && IsConfigurationValid(config);
    }

    private async Task<TimeSpan> RunWorkAsync(CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var currentRunTime = DateTime.UtcNow;

        await RunOnceAsync(currentRunTime, stoppingToken);

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private async Task RunOnceAsync(DateTime currentRunTime, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var syncUseCases = scope.ServiceProvider.GetRequiredService<ISyncUseCases>();

        var hasActiveSync = await syncUseCases.IsThereAlreadyActiveSync(stoppingToken);

        if (!hasActiveSync.IsSuccess)
        {
            _logger.LogWarning("Попытка запуска фоновой синхронизации во время выполнения другой синхронизации.");
            return;
        }

        var (syncResult, sendingModel) = await syncUseCases.StartDataCollectionAndWaitAsync("Фоновый сервис", stoppingToken);

        if (syncResult.IsSuccess)
        {
            _logger.LogInformation("Получены изменения за период с {From} UTC по {To} UTC",
                _lastRunTime.ToString("dd.MM.yyyy HH:mm:ss:f"),
                currentRunTime.ToString("dd.MM.yyyy HH:mm:ss:f"));

            _lastRunTime = currentRunTime;
        }
        else
        {
            _logger.LogError("Не удалось получить изменения за период с {From} по {To}. Сообщение об ошибке: {ErrorMessage}",
                _lastRunTime.ToString("dd.MM.yyyy HH:mm:ss:f"),
                currentRunTime.ToString("dd.MM.yyyy HH:mm:ss:f"),
                syncResult.ErrorMessage);
        }
    }

    private async Task NotifyAboutError(string errorMessage, Exception ex, string subject)
    {
        await _emailTaskQueue.QueueBackgroundWorkItem((sp, stopTkn) =>
        {
            var emailService = sp.GetRequiredService<IEmailService>();
            return emailService.ComposeAndSendAsync(errorMessage, ex, subject);
        });
    }

    private async Task NotifyAboutError(string errorMessage, string subject)
    {
        await _emailTaskQueue.QueueBackgroundWorkItem((sp, stopTkn) =>
        {
            var emailService = sp.GetRequiredService<IEmailService>();
            return emailService.ComposeAndSendAsync(errorMessage, subject);
        });
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Вызван метод BackgroundService.StopAsync(). {name} останавливается...", nameof(PolynomApiSyncBackgroundService));
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _onChangeRegistration.Dispose();
        _configChangedChannel.Writer.TryComplete();
        base.Dispose();
    }

    /// <summary>
    /// Блокируется на канале до получения <c>true</c> (т.е. до момента, когда файл конфигурации
    /// изменится и будет содержать корректно заполненные значения).
    /// Каждый полученный <c>false</c> логируется.
    /// </summary>
    private async Task<bool> WaitForValidConfigAsync(CancellationToken ct)
    {
        try
        {
            await foreach (bool isValid in _configChangedChannel.Reader.ReadAllAsync(ct))
            {
                if (isValid) return true;

                _logger.LogInformation("Файл конфигурации изменился, но всё ещё содержит значения по умолчанию. Продолжаем ожидание...");
            }
        }
        catch (OperationCanceledException) { /* stoppingToken — выходим */ }

        return false;
    }

    /// <summary>
    /// Проверяет, что конфигурация заполнена осмысленными значениями
    /// (т.е. ни одно поле не осталось равным значению по умолчанию для своего типа).
    /// </summary>
    private static bool IsConfigurationValid(AppConfiguration? config)
    {
        if (config is null) return false;

        return IsTargetReferenceNodeValid(config.TargetReferenceNode)
            && IsRabbitMqQueuesValid(config.RabbitMqQueues)
            && IsRabbitMqRetryParamsValid(config.RabbitMqRetryParams)
            //&& IsPolynomConfigValid(config.PolynomConfig)
            && IsPolynomApiSyncOptionsValid(config.PolynomApiSyncOptions)
            && IsEmailNotificationsValid(config.EmailNotifications);
    }

    private static bool IsTargetReferenceNodeValid(TargetReferenceNode? node) =>
        node is not null
        && node.TargetReferenceNodeObjectId != 0
        && node.TargetReferenceNodeTypeId != 0
        && !string.IsNullOrWhiteSpace(node.TargetReferenceNodeName);

    private static bool IsRabbitMqQueuesValid(RabbitMqQueues? queues) =>
        queues is not null
        && !string.IsNullOrEmpty(queues.NsiTransferExchangeName)
        && !string.IsNullOrWhiteSpace(queues.PolynomSearchResultsQueueName);

    private static bool IsRabbitMqRetryParamsValid(RabbitMqRetryParams? p) =>
        p is not null
        && p.MaxRetryAttempts > 0
        && p.RetryIntervalInSeconds > 0
        && p.ConfirmationTimeOutInSeconds > 0;

    //private static bool IsPolynomConfigValid(PolynomConfig? c) =>
    //    c is not null
    //    && !string.IsNullOrWhiteSpace(c.Address)
    //    && !string.IsNullOrWhiteSpace(c.DbName)
    //    && !string.IsNullOrWhiteSpace(c.TimeZoneId);

    private static bool IsPolynomApiSyncOptionsValid(PolynomApiSyncOptions? o) =>
        o is not null && o.StartSyncWithIntervalMinutes > 0;

    private static bool IsEmailNotificationsValid(EmailNotificationsOptions? o) =>
        o is not null
        && o.ErrorRecipients.Length > 0
        && o.ErrorRecipients.All(r => !string.IsNullOrWhiteSpace(r));
}