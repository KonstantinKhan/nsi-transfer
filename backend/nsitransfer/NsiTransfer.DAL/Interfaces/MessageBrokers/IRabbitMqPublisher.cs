using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.DAL.Interfaces.MessageBrokers;

/// <summary>
/// Интерфейс для публикации сообщений в RabbitMQ
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>
    /// Опубликовать сообщение в указанную очередь
    /// </summary>
    /// <param name="queueName">Имя очереди</param>
    /// <param name="contentType">Тип контента (MIME-type, например, application/json)</param>
    /// <param name="message">Текст сообщения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Task для асинхронной операции</returns>
    Task<RabbitMqPublishingResultEnum> PublishAsync(
        string exchangeName,
        string queueName, 
        string contentType, 
        string messageId, 
        string message, 
        CancellationToken cancellationToken = default);
}
