using System.Text;
using Microsoft.Extensions.Options;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Interfaces.Email;

namespace NsiTransfer.BLL.Services;

internal class EmailService : IEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly IOptionsMonitor<EmailNotificationsOptions> _emailNotifyOptions;

    public EmailService(IEmailSender emailSender, IOptionsMonitor<EmailNotificationsOptions> emailNotifyOptions)
    {
        _emailSender = emailSender;
        _emailNotifyOptions = emailNotifyOptions;
    }

    public async Task ComposeAndSendAsync<T>(Result<T> result, string subject)
    {
        var htmlBody = BuildResultHtml(result);
        await _emailSender.SendEmailAsync(_emailNotifyOptions.CurrentValue.ErrorRecipients, subject, htmlBody);
    }

    public async Task ComposeAndSendAsync(Result result, string subject)
    {
        var htmlBody = BuildResultHtml(result);
        await _emailSender.SendEmailAsync(_emailNotifyOptions.CurrentValue.ErrorRecipients, subject, htmlBody);
    }

    public async Task ComposeAndSendAsync(string message, string subject)
    {
        var htmlBody = BuildMessageHtml(message);
        await _emailSender.SendEmailAsync(_emailNotifyOptions.CurrentValue.ErrorRecipients, subject, htmlBody);
    }

    public async Task ComposeAndSendAsync(Exception exception, string subject)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var htmlBody = BuildExceptionHtml(exception);
        await _emailSender.SendEmailAsync(_emailNotifyOptions.CurrentValue.ErrorRecipients, subject, htmlBody);
    }

    public async Task ComposeAndSendAsync(string message, Exception exception, string subject)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var htmlBody = BuildMessageAndExceptionHtml(message, exception);
        await _emailSender.SendEmailAsync(_emailNotifyOptions.CurrentValue.ErrorRecipients, subject, htmlBody);
    }

    #region HTML Builders

    private string BuildResultHtml<T>(Result<T> result)
    {
        var sb = new StringBuilder();
        sb.Append(GetHtmlHeader());

        if (result.IsSuccess)
        {
            sb.Append(@"
                <div class='success-box'>
                    <h2>✅ Операция выполнена успешно</h2>
                </div>
                <div class='info-box'>
                    <p>Данные операции успешно обработаны.</p>
                </div>");
        }
        else
        {
            sb.Append(@"
                <div class='error-box'>
                    <h2>❌ Операция завершилась с ошибкой</h2>
                </div>");

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                sb.Append($@"
                    <div class='details-box'>
                        <h3>Детали ошибки:</h3>
                        <pre class='error-message'>{EscapeHtml(result.ErrorMessage)}</pre>
                    </div>");
            }
        }

        sb.Append(GetHtmlFooter());
        return sb.ToString();
    }

    private string BuildResultHtml(Result result)
    {
        var sb = new StringBuilder();
        sb.Append(GetHtmlHeader());

        if (result.IsSuccess)
        {
            sb.Append(@"
                <div class='success-box'>
                    <h2>✅ Операция выполнена успешно</h2>
                </div>
                <div class='info-box'>
                    <p>Операция успешно завершена.</p>
                </div>");
        }
        else
        {
            sb.Append(@"
                <div class='error-box'>
                    <h2>❌ Операция завершилась с ошибкой</h2>
                </div>");

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                sb.Append($@"
                    <div class='details-box'>
                        <h3>Детали ошибки:</h3>
                        <pre class='error-message'>{EscapeHtml(result.ErrorMessage)}</pre>
                    </div>");
            }
        }

        sb.Append(GetHtmlFooter());
        return sb.ToString();
    }

    private string BuildMessageHtml(string message)
    {
        var sb = new StringBuilder();
        sb.Append(GetHtmlHeader());

        sb.Append($@"
            <div class='info-box'>
                <h2>📨 Сообщение</h2>
                <div class='message-content'>
                    {EscapeHtml(message).Replace("\n", "<br/>")}
                </div>
            </div>");

        sb.Append(GetHtmlFooter());
        return sb.ToString();
    }

    private string BuildExceptionHtml(Exception exception)
    {
        var sb = new StringBuilder();
        sb.Append(GetHtmlHeader());

        sb.Append($@"
            <div class='error-box'>
                <h2>⚠️ Произошло исключение</h2>
                    <tr>
                        <td><strong>Время:</strong></td>
                        <td>{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}</td>
                    </tr>
            </div>");

        sb.Append($@"
            <div class='details-box'>
                <h3>Информация об исключении:</h3>
                <table class='info-table'>
                    <tr>
                        <td><strong>Тип:</strong></td>
                        <td>{EscapeHtml(exception.GetType().FullName ?? "Unknown")}</td>
                    </tr>
                    <tr>
                        <td><strong>Сообщение:</strong></td>
                        <td>{EscapeHtml(exception.Message)}</td>
                    </tr>
                </table>");

        if (exception.StackTrace != null)
        {
            sb.Append($@"
                <h3>Stack Trace:</h3>
                <pre class='stack-trace'>{EscapeHtml(exception.StackTrace)}</pre>
            </div>");
        }
        else
        {
            sb.Append("</div>");
        }

        if (exception.InnerException != null)
        {
            sb.Append(@"
                <div class='details-box'>
                    <h3>Внутреннее исключение:</h3>
                    <pre class='error-message'>" +
                    EscapeHtml(exception.InnerException.ToString()) +
                    @"</pre>
                </div>");
        }

        sb.Append(GetHtmlFooter());
        return sb.ToString();
    }

    private string BuildMessageAndExceptionHtml(string message, Exception exception)
    {
        var sb = new StringBuilder();
        sb.Append(GetHtmlHeader());

        sb.Append($@"
            <div class='info-box'>
                <h2>📨 Сообщение</h2>
                <div class='message-content'>
                    {EscapeHtml(message).Replace("\n", "<br/>")}
                </div>
            </div>");

        sb.Append($@"
            <div class='error-box'>
                <h2>⚠️ Произошло исключение</h2>
                    <tr>
                        <td><strong>Время:</strong></td>
                        <td>{DateTime.Now:yyyy-MM-dd HH:mm:ss:fff}</td>
                    </tr>
            </div>");

        sb.Append($@"
            <div class='details-box'>
                <h3>Информация об исключении:</h3>
                <table class='info-table'>
                    <tr>
                        <td><strong>Тип:</strong></td>
                        <td>{EscapeHtml(exception.GetType().FullName ?? "Unknown")}</td>
                    </tr>
                    <tr>
                        <td><strong>Сообщение:</strong></td>
                        <td>{EscapeHtml(exception.Message)}</td>
                    </tr>
                </table>");

        if (exception.StackTrace != null)
        {
            sb.Append($@"
                <h3>Stack Trace:</h3>
                <pre class='stack-trace'>{EscapeHtml(exception.StackTrace)}</pre>
            </div>");
        }
        else
        {
            sb.Append("</div>");
        }

        if (exception.InnerException != null)
        {
            sb.Append(@"
                <div class='details-box'>
                    <h3>Внутреннее исключение:</h3>
                    <pre class='error-message'>" +
                    EscapeHtml(exception.InnerException.ToString()) +
                    @"</pre>
                </div>");
        }

        sb.Append(GetHtmlFooter());
        return sb.ToString();
    }

    #endregion

    #region HTML Helpers

    private string GetHtmlHeader()
    {
        return @"
<!DOCTYPE html>
<html lang='ru'>
<head>
<meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<style>
    body {
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        background-color: #f4f4f4;
        margin: 0;
        padding: 20px;
        color: #333;
    }
    .container {
        max-width: 800px;
        margin: 0 auto;
        background-color: #ffffff;
        border-radius: 8px;
        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        padding: 30px;
    }
    .success-box {
        background-color: #d4edda;
        border-left: 4px solid #28a745;
        padding: 15px;
        margin-bottom: 20px;
        border-radius: 4px;
    }
    .success-box h2 {
        color: #155724;
        margin: 0;
    }
    .error-box {
        background-color: #f8d7da;
        border-left: 4px solid #dc3545;
        padding: 15px;
        margin-bottom: 20px;
        border-radius: 4px;
    }
    .error-box h2 {
        color: #721c24;
        margin: 0;
    }
    .info-box {
        background-color: #d1ecf1;
        border-left: 4px solid #17a2b8;
        padding: 15px;
        margin-bottom: 20px;
        border-radius: 4px;
    }
    .info-box h2 {
        color: #0c5460;
        margin: 0 0 10px 0;
    }
    .details-box {
        background-color: #f8f9fa;
        border: 1px solid #dee2e6;
        padding: 15px;
        margin-bottom: 20px;
        border-radius: 4px;
    }
    .details-box h3 {
        color: #495057;
        margin-top: 0;
        border-bottom: 2px solid #dee2e6;
        padding-bottom: 10px;
    }
    .error-message {
        background-color: #fff;
        border: 1px solid #dc3545;
        padding: 10px;
        border-radius: 4px;
        color: #721c24;
        white-space: pre-wrap;
        font-family: 'Courier New', monospace;
        font-size: 14px;
        overflow-x: auto;
    }
    .stack-trace {
        background-color: #fff;
        border: 1px solid #6c757d;
        padding: 10px;
        border-radius: 4px;
        color: #212529;
        white-space: pre-wrap;
        font-family: 'Courier New', monospace;
        font-size: 12px;
        overflow-x: auto;
        max-height: 400px;
        overflow-y: auto;
    }
    .message-content {
        padding: 10px;
        line-height: 1.6;
    }
    .info-table {
        width: 100%;
        border-collapse: collapse;
        margin: 10px 0;
    }
    .info-table td {
        padding: 8px;
        border-bottom: 1px solid #dee2e6;
    }
    .info-table td:first-child {
        width: 150px;
        color: #495057;
    }
    .footer {
        margin-top: 30px;
        padding-top: 20px;
        border-top: 1px solid #dee2e6;
        text-align: center;
        color: #6c757d;
        font-size: 12px;
    }
</style>
</head>
<body>
<div class='container'>";
    }

    private string GetHtmlFooter()
    {
        return $@"
    <div class='footer'>
        <p>Это автоматическое уведомление. Пожалуйста, не отвечайте на это письмо.</p>
        <p>Отправлено: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>
</div>
</body>
</html>";
    }

    private string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    #endregion
}
