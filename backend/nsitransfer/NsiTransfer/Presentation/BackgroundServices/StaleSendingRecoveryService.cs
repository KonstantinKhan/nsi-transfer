using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Interfaces.Db;

namespace NsiTransfer.Presentation.BackgroundServices;

/// <summary>
/// При старте приложения синхронизация могла быть прервана на середине предыдущим завершением процесса
/// (рестарт/краш) — Sending остаётся в Initiated/Pending с ActiveMarker=true навсегда, потому что снять
/// этот статус может только код, который выполнялся в уже погибшем процессе (см. Sending.ActiveStatuses,
/// SyncUseCases.PendingSending). Из-за уникального индекса на ActiveMarker такой "зависший" Sending
/// блокирует вообще все последующие попытки синхронизации (SyncUseCases.CreateSendingIfNoneActiveAsync).
/// Регистрировать до PolynomApiSyncBackgroundService в Program.cs: хост-сервисы стартуют последовательно,
/// это гарантирует, что блокировка снимается раньше, чем успеет создаться новый Sending.
/// </summary>
public sealed class StaleSendingRecoveryService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleSendingRecoveryService> _logger;

    public StaleSendingRecoveryService(IServiceScopeFactory scopeFactory, ILogger<StaleSendingRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var staleSendings = await unitOfWork.Sendings.GetAllAsync(
            predicate: s => s.ActiveMarker == true,
            cancellationToken: cancellationToken);

        if (staleSendings.Count == 0)
            return;

        foreach (var sending in staleSendings)
        {
            _logger.LogWarning(
                "Sending {SendingId} остался в статусе {Status} с ActiveMarker=true после предыдущего запуска приложения. " +
                "Перевожу в {ErrorStatus}, чтобы снять блокировку новых попыток синхронизации.",
                sending.Id, sending.StatusId, SendingStatusEnum.ErrorUnknown);

            sending.SetStatus(SendingStatusEnum.ErrorUnknown);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
