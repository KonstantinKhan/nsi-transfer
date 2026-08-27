using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.DAL.Db.Entities;

public class EmailMessage
{
    public long Id { get; set; }
    public DateTime SentAt { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public EmailStatus Status { get; set; }

    public ICollection<EmailRecipient> Recipients { get; set; }
    public EmailFailure? Failure { get; set; }  // null если отправка успешна
}
