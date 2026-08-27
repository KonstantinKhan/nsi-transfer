using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Db.Entities;

namespace NsiTransfer.DAL.Interfaces.Db;

/// <summary>
/// Интерфейс репозитория для работы с сущностью Sending
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// Создать и сохранить новую сущность Sending
    /// </summary>
    Task<Result<long>> CreateAsync(Message sending, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить Sending по ID
    /// </summary>
    Task<Result<Message>> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновить сущность Sending
    /// </summary>
    Task<Result> UpdateAsync(Message sending, CancellationToken cancellationToken = default);
}

