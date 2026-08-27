using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;
using NsiTransfer.DAL.Interfaces.Email;

namespace NsiTransfer.DAL.Network.Email;

internal class EmailSender : IEmailSender
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptionsMonitor<SmtpSettings> _optionsMonitor;

    public EmailSender(IUnitOfWork unitOfWork, IOptionsMonitor<SmtpSettings> optionsMonitor)
    {
        _unitOfWork = unitOfWork;
        _optionsMonitor = optionsMonitor;
    }

    public async Task SendEmailAsync(string[] toEmail, string subject, string htmlBody)
    {
        var settings = _optionsMonitor.CurrentValue;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
        foreach (var email in toEmail)
        {
            message.To.Add(new MailboxAddress("", email));
        }

        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        var recipients = await ResolveRecipientsAsync(toEmail);

        var emailMessage = new EmailMessage
        {
            SentAt = DateTime.UtcNow,
            Subject = subject,
            Body = htmlBody,
            Status = EmailStatus.Sent,
            Recipients = recipients
        };

        using var client = new SmtpClient();
        try
        {
            var secureSocketOptions = settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            if (settings.Port == 465) secureSocketOptions = SecureSocketOptions.SslOnConnect;

            await client.ConnectAsync(settings.Server, settings.Port, secureSocketOptions);
            await client.AuthenticateAsync(settings.Username, settings.Password);
            await client.SendAsync(message);
        }
        catch (Exception ex)
        {
            emailMessage.Status = EmailStatus.Failed;
            emailMessage.Failure = new EmailFailure { ErrorMessage = ex.Message };
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);

            await _unitOfWork.EmailMessages.AddAsync(emailMessage);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private async Task<List<EmailRecipient>> ResolveRecipientsAsync(string[] toEmails)
    {
        var recipients = new List<EmailRecipient>();
        foreach (var email in toEmails)
        {
            var recipient = await _unitOfWork.EmailRecipients.FirstOrDefaultAsync(rec => rec.EmailAddress.Equals(email))
                            ?? new EmailRecipient { EmailAddress = email };
            recipients.Add(recipient);
        }
        return recipients;
    }
}
