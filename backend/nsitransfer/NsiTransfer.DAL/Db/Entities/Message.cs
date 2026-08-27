using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.DAL.Db.Entities;

public class Message
{
    public long Id { get; set; }
    public DateTime StartedCollectionFromPolynomAt { get; set; }
    public DateTime? FinishedCollectionFromPolynomAt { get; set; }
    public DateTime? SentAtQueue { get; set; }
    
    public string? SerializedMessage { get; set; }
    public RabbitMqPublishingResultEnum PublishingResultId { get; set; }

    public MessageTypeEnum MessageType { get; set; }

    public Guid SendingId { get; set; }
    public Sending Sending { get; set; }

    //public int PolynomObjectsCount { get; set; }

    public ICollection<PolynomObject> PolynomObjects { get; set; }
    public MessageFailure? MessageFailure { get; set; }
}
