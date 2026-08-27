using System.ComponentModel;

namespace NsiTransfer.Contract.Models.Enums;

public enum RabbitMqPublishingResultEnum
{
    [Description("Неизвестная ошибка")]
    Unknown = 0,

    [Description("Подтверждение от RabbitMQ")]
    Ack = 1,

    [Description("Отрицательное подтверждение от RabbitMQ")]
    Nack = 2,

    [Description("Превышено время ожидания подтверждения от RabbitMQ")]
    TimedOut = 3,

    [Description("Подключение к RabbitMQ было заблокировано")]
    ConnectionBlocked = 4,

    [Description("Не удалось подключиться к RabbitMQ или опубликовать сообщение в очередь RabbitMQ после нескольких попыток")]
    Failed = 5,

    [Description("Не удалось собрать объекты со свойствами перед формированием самого сообщения для отправки")]
    FailedDuringInformationCollection = 6
}
