using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.Contract.Models;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;

namespace NsiTransfer.BLL.Interfaces.UseCases;

public interface ISyncUseCases
{
    Task<Result> IsThereAlreadyActiveSync(CancellationToken cancellationToken = default);
    Task<(Result Result, SendingModel? Sending)> StartDataCollectionAndWaitAsync(string initiatorName, CancellationToken cancellationToken = default);
    Task<(Result Result, SendingModel? Sending)> StartDataCollectionInBackgroundAsync(string initiatorName, CancellationToken cancellationToken = default);
    Task<Result> ContinueDataCollectionAsync(Guid sendingId, CancellationToken cancellationToken);

    /// <summary>
    /// Запускает в фоне полную переиндексацию персистентного кеша последних кодов классификатора по всем группам
    /// целевого справочника. Отказывает, если в данный момент выполняется активная синхронизация.
    /// </summary>
    Task<Result> RebuildGroupCodeCacheInBackgroundAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SendingModel>> GetAllSendingsAsync(CancellationToken cancellationToken = default);
    Task<SendingModel?> GetSending(Guid sendingId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SendingModel> Items, bool HasMore)> GetSendingsPageAsync(int pageSize, SendingsCursor? cursor, CancellationToken cancellationToken = default);
}
