using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NsiTransfer.BLL.Interfaces.Services;

namespace NsiTransfer.BLL.BackgroundServices;

internal class EmailTaskConsumer : BackgroundService
{
    private readonly IBackgroundTaskQueue _emailTaskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailTaskConsumer> _logger;

    public EmailTaskConsumer(
        IEnumerable<IBackgroundTaskQueue> emailTaskQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailTaskConsumer> logger)
    {
        _emailTaskQueue = emailTaskQueue.FirstOrDefault(q => q.QueueName == "email") ?? throw new ArgumentException("Service for queue of type 'email' not found.");
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Dequeue из очереди, получение отдельного scope, который будет использоваться для выполнения задачи из queue, передача в queue нужных параметров, выполнение

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _emailTaskQueue.DequeueAsync(stoppingToken);

                var currentScope = _scopeFactory.CreateScope();

                await workItem(currentScope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении фоновой задачи с использованием e-mail.");
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Вызван метод BackgroundService.StopAsync(). {name} останавливается...", nameof(EmailTaskConsumer));
        return base.StopAsync(cancellationToken);
    }
}
