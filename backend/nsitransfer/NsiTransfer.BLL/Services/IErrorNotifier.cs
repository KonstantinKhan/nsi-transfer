using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Services;

/// <summary>
/// Централизованный механизм отправки email-уведомлений об ошибках.
/// Инкапсулирует постановку задач в фоновую очередь email.
/// </summary>
public interface IErrorNotifier
{
    Task NotifyAboutErrorAsync<T>(Result<T> result, string subject);
    Task NotifyAboutErrorAsync(string errorMessage, Exception exception, string subject);
    Task NotifyAboutErrorAsync(string errorMessage, string subject);
}
