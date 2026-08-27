namespace NsiTransfer.Contract.Models.DTO;

public class MessageFailureModel
{
    public long MessageId { get; set; }
    public DateTime FailedAt { get; set; }
    public string FailureDescription { get; set; }
    public string FailureReasonTitle { get; set; }
}