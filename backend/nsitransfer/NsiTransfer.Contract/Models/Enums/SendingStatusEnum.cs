using System.ComponentModel;

namespace NsiTransfer.Contract.Models.Enums;

public enum SendingStatusEnum
{
    [Description("Неизвестный статус, отсутствие статуса")]
    Unknown = 0,

    [Description("Инициировано начало отправления")]
    Initiated = 1,

    [Description("В процессе сборки и отправления сообщений")]
    Pending = 2,

    [Description("Все сообщения из отправления успешно собраны и отправлены в брокер сообщений")]
    Completed = 3,

    [Description("Сформировано пустое сообщение. Новых изменений в системе Полином не было найдено")]
    EmptySending = 4,

    [Description("Неизвестная ошибка")]
    ErrorUnknown = 100,

    [Description("Ошибка при подготовке отправления")]
    ErrorOccuredWhilePreparing = 101,

    [Description("Ошибка при сборе данных для одного из сообщений в отправлении")]
    ErrorOccuredWhileCollectingDataForMessage = 102,

    [Description("Ошибка при публикации одного из сообщений в отправлении")]
    ErrorOccuredWhilePublishingMessage = 103
}