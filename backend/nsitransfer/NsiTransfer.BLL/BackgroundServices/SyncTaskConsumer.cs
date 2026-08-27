using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NsiTransfer.BLL.Interfaces.Services;

namespace NsiTransfer.BLL.BackgroundServices;

internal class SyncTaskConsumer : BackgroundService
{
    private readonly IBackgroundTaskQueue _syncTaskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SyncTaskConsumer> _logger;

    public SyncTaskConsumer(
        IEnumerable<IBackgroundTaskQueue> syncTaskQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<SyncTaskConsumer> logger)
    {
        _syncTaskQueue = syncTaskQueue.FirstOrDefault(q => q.QueueName == "sync") ?? throw new ArgumentException("Service for queue of type 'sync' not found.");
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _syncTaskQueue.DequeueAsync(stoppingToken);

                var currentScope = _scopeFactory.CreateScope();

                await workItem(currentScope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении фоновой задачи синхронизации.");
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Вызван метод BackgroundService.StopAsync(). {name} останавливается...", nameof(SyncTaskConsumer));
        return base.StopAsync(cancellationToken);
    }
}