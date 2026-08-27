namespace NsiTransfer.DAL.Db.Entities;

public class EmailRecipient
{
    public long Id { get; set; }
    public string EmailAddress { get; set; }

    public ICollection<EmailMessage> Messages { get; set; }
}
