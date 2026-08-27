using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.Contract.Models.DTO;

public class MessageModel
{
    public long Id { get; set; }
    public DateTime StartedCollectionFromPolynomAt { get; set; }
    public DateTime? FinishedCollectionFromPolynomAt { get; set; }
    public DateTime? SentAtQueue { get; set; }
    
    public int PolynomObjectsAmountInMessage { get; set; }
    public RabbitMqPublishingResultEnum PublishingResultId { get; set; }

    public MessageTypeEnum MessageType { get; set; }

    public MessageFailureModel? MessageFailure { get; set; }
}