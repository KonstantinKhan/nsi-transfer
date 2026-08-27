using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.Contract.Models.DTO;

public class SendingModel
{
    public Guid Id { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string InitiatorName { get; set; }
    public Enums.SendingStatusEnum StatusId { get; set; }
    public string TargetReferenceNodeName { get; set; }

    public List<MessageModel> Messages { get; set; }
}