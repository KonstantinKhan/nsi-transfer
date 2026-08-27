namespace NsiTransfer.DAL.Interfaces.Email;

public interface IEmailSender
{
    Task SendEmailAsync(string[] toEmail, string subject, string htmlBody);
}
