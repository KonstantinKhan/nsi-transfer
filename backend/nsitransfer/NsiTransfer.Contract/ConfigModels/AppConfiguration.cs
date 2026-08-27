using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Contract.ConfigModels;

public class AppConfiguration
{
    [Required(ErrorMessage = "Параметр '" + nameof(TargetReferenceNode) + "' (Целевой узел) обязателен.")]
    public TargetReferenceNode TargetReferenceNode { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(RabbitMqQueues) + "' (Очереди RabbitMq) обязателен.")]
    public RabbitMqQueues RabbitMqQueues { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(RabbitMqRetryParams) + "' (Параметры повторов RabbitMq) обязателен.")]
    public RabbitMqRetryParams RabbitMqRetryParams { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(PolynomApiSyncOptions) + "' (Опции синхронизации Polynom API) обязателен.")]
    public PolynomApiSyncOptions PolynomApiSyncOptions { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(EmailNotifications) + "' (Настройки email-уведомлений) обязателен.")]
    public EmailNotificationsOptions EmailNotifications { get; set; }
}


public class TargetReferenceNode
{
    /// <summary>
    /// ObjectId справочника, в котором будет происходить поиск по умолчанию, то есть область поиска.
    /// </summary>
    public int TargetReferenceNodeObjectId { get; set; }

    /// <summary>
    /// TypeId справочника, в котором будет происходить поиск по умолчанию, то есть область поиска.
    /// Должен иметь корреляцию с Ascon.Polynom.Web.Api.Data.Interfaces.Enums.IdentifiableObjectType из Ascon.Polynom.Web.Api.Data.dll
    /// </summary>
    public int TargetReferenceNodeTypeId { get; set; }
    public string TargetReferenceNodeName { get; set; } = string.Empty;
}

// Сами классы настроек
public class RabbitMqQueues
{
    [Required(ErrorMessage = "Параметр '" + nameof(NsiTransferExchangeName) + "' (Имя обменника для NsiTransfer внутри RabbitMQ) обязателен.")]
    public string NsiTransferExchangeName { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(PolynomSearchResultsQueueName) + "' (Имя очереди результатов поиска) обязателен.")]
    public string PolynomSearchResultsQueueName { get; set; } = string.Empty;
}

public class RabbitMqRetryParams
{
    [Range(1, int.MaxValue, ErrorMessage = "Параметр '" + nameof(MaxRetryAttempts) + "' (Максимальное количество повторных попыток) должен быть больше 0.")]
    public int MaxRetryAttempts { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Параметр '" + nameof(RetryIntervalInSeconds) + "' (Интервал между повторными попытками) должен быть больше 0.")]
    public int RetryIntervalInSeconds { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Параметр '" + nameof(ConfirmationTimeOutInSeconds) + "' (Таймаут подтверждения) должен быть больше 0.")]
    public int ConfirmationTimeOutInSeconds { get; set; }
}

public class PolynomApiSyncOptions
{
    [Range(1, int.MaxValue, ErrorMessage = "Параметр '" + nameof(StartSyncWithIntervalMinutes) + "' (Интервал между вызовами фонового процесса синхронизации) должен быть больше 0.")]
    public int StartSyncWithIntervalMinutes { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(ConceptNameForClassificationData) + "' (Название понятия, которое будет являться родительским для свойств для классификатора) обязателен для заполнения. Тип параметра - строка.")]
    public string ConceptNameForClassificationData { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(ClassificationCodePropertyName) + "' (Название свойства, которое будет в себе хранить код классификатора) обязателен для заполнения. Тип параметра - строка.")]
    public string ClassificationCodePropertyName { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(OwnContractName) + "' (Название понятия, содержащего собственные свойства родительской группы) обязателен для заполнения. Тип параметра - строка.")]
    public string OwnContractName { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(MinCodePropertyName) + "' (Название свойства, содержащего минимальное значение кода классификатора) обязателен для заполнения. Тип параметра - строка.")]
    public string MinCodePropertyName { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(MaxCodePropertyName) + "' (Название свойства, содержащего максимальное значение кода классификатора) обязателен для заполнения. Тип параметра - строка.")]
    public string MaxCodePropertyName { get; set; }
}

public class EmailNotificationsOptions
{
    [Required(ErrorMessage = "Параметр '" + nameof(ErrorRecipients) + "' (Email адреса получателей сообщений об ошибках) обязателен для заполнения. Тип параметра - массив строк.")]
    public string[] ErrorRecipients { get; set; } = [];
}
