namespace NsiTransfer.DAL.Db.Entities;

public class EmailFailure
{
    public long Id { get; set; }
    public string ErrorMessage { get; set; }

    public long EmailMessageId { get; set; }
    public EmailMessage EmailMessage { get; set; }
}
