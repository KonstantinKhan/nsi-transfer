using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Interfaces.Services;

public interface IEmailService
{
    /// <summary>
    /// Отправляет email с результатом операции (с данными)
    /// </summary>
    Task ComposeAndSendAsync<T>(Result<T> result, string subject);

    /// <summary>
    /// Отправляет email с результатом операции (без данных)
    /// </summary>
    Task ComposeAndSendAsync(Result result, string subject);

    /// <summary>
    /// Отправляет email с простым текстовым сообщением
    /// </summary>
    Task ComposeAndSendAsync(string message, string subject);

    /// <summary>
    /// Отправляет email с информацией об исключении
    /// </summary>
    Task ComposeAndSendAsync(Exception exception, string subject);

    /// <summary>
    /// Отправляет email с сообщением и исключением
    /// </summary>
    Task ComposeAndSendAsync(string message, Exception exception, string subject);
}