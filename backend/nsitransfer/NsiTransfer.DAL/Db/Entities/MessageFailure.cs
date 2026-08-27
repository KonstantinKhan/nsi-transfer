namespace NsiTransfer.DAL.Db.Entities;

public class MessageFailure
{
    public long Id { get; set; }
    public DateTime FailedAt { get; set; }
    public string FailureDescription { get; set; }
    public string FailureReasonTitle { get; set; }

    public long MessageId { get; set; }
    public Message Message { get; set; }
}