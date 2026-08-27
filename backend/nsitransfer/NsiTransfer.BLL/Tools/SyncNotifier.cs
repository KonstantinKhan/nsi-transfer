using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.Contract.Models.EventArgs;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace NsiTransfer.BLL.Tools;

internal class SyncNotifier : ISyncNotifier
{
    // Сами каналы, по которым передаются сообщения
    private readonly ConcurrentDictionary<Guid, Channel<SyncEvent>> _syncChannels = new();


    // Поток всех событий из всех активных каналов
    private readonly Channel<(Guid SendingId, SyncEvent SyncEvent)> _allSyncEventsStream 
        = Channel.CreateUnbounded<(Guid SendingId, SyncEvent SyncEvent)>(new UnboundedChannelOptions { SingleWriter = false });


    public event SyncEventProducer? SyncEventsEvent;
    protected virtual void OnSyncEventsEvent(SyncEventEventArgs e)
    {
        // стандартный паттерн: копируем в локальную переменную, чтобы избежать гонки при многопоточности
        var handler = SyncEventsEvent;
        handler?.Invoke(this, e);
    }

    private readonly IBackgroundTaskQueue _emailTaskQueue;
    private readonly ILogger<SyncNotifier> _logger;





    public SyncNotifier(IEnumerable<IBackgroundTaskQueue> backQueues, ILogger<SyncNotifier> logger)
    {
        _logger = logger;
        _emailTaskQueue = backQueues.FirstOrDefault(q => q.QueueName.Equals("email", StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Service for queue of type 'email' not found.");

        // Запускаем поток считывания всех событий обо всех Sendings
        _ = StreamAllEvents(default);
    }

    public void InitializeStream(Guid sendingId)
    {
        var channel = Channel.CreateBounded<SyncEvent>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _syncChannels.TryAdd(sendingId, channel);


        _ = HandleNewSyncChannel(sendingId, channel);
    }

    public bool TryGetReader(Guid sendingId, out ChannelReader<SyncEvent> reader)
    {
        if (_syncChannels.TryGetValue(sendingId, out var channel))
        {
            reader = channel.Reader;
            return true;
        }

        reader = null!;
        return false;
    }

    public async IAsyncEnumerable<SyncEvent> StreamEvents(Guid sendingId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_syncChannels.TryGetValue(sendingId, out var channel))
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            // Если синхронизация уже завершилась или не найдена
            yield return new SyncEvent("Closed", new { Message = "Stream not found or already completed." });
        }
    }

    public async Task NotifyAsync(Guid sendingId, SyncEvent syncEvent, CancellationToken cancellationToken)
    {
        if (_syncChannels.TryGetValue(sendingId, out var channel))
        {
            await channel.Writer.WriteAsync(syncEvent, cancellationToken);
        }
    }

    public void Complete(Guid sendingId)
    {
        if (_syncChannels.TryRemove(sendingId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    private async Task StreamAllEvents(CancellationToken cancellationToken)
    {
        var allSyncEventsReader = _allSyncEventsStream.Reader;

        while (!cancellationToken.IsCancellationRequested)
        {
            var readTask = allSyncEventsReader.WaitToReadAsync(cancellationToken).AsTask();

            var completed = await Task.WhenAny(
                readTask,
                Task.Delay(TimeSpan.FromSeconds(15), cancellationToken));

            if (completed != readTask)
            {
                var args = new SyncEventEventArgs
                {
                    SendingId = Guid.Empty,
                    SyncEvent = new SyncEvent(": heartbeat\n\n", null)
                };

                OnSyncEventsEvent(args);
                continue;
            }

            if (!await readTask)
            {
                var args = new SyncEventEventArgs
                {
                    SendingId = Guid.Empty,
                    SyncEvent = new SyncEvent("Closed", new { message = "All streams completed" })
                };

                OnSyncEventsEvent(args);
                break;
            }

            while (allSyncEventsReader.TryRead(out var syncEvent))
            {
                var args = new SyncEventEventArgs
                {
                    SendingId = syncEvent.SendingId,
                    SyncEvent = syncEvent.SyncEvent
                };

                OnSyncEventsEvent(args);
            }
        }
    }

    private async Task HandleNewSyncChannel(Guid sendingId, Channel<SyncEvent> channel)
    {
        var channelStream = channel.Reader.ReadAllAsync();
        try
        {
            await foreach (var syncEvent in channelStream)
            {
                _allSyncEventsStream.Writer.TryWrite((sendingId, syncEvent));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var err = $"Ошибка в процессе обработки событий синхронизации отправление с SendingId \'{sendingId}\'";
            _logger.LogError(ex, err);
            await _emailTaskQueue.QueueBackgroundWorkItem((sp, stopTkn) =>
            {
                var emailService = sp.GetRequiredService<IEmailService>();
                return emailService.ComposeAndSendAsync(err, ex, err);
            });
        }
    }
}