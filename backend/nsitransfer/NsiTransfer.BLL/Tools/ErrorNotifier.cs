using Microsoft.Extensions.DependencyInjection;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Services;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Tools;

internal sealed class ErrorNotifier : IErrorNotifier
{
    private readonly IBackgroundTaskQueue _emailTaskQueue;

    public ErrorNotifier(IEnumerable<IBackgroundTaskQueue> backgroundTaskQueues)
    {
        _emailTaskQueue = backgroundTaskQueues
            .FirstOrDefault(q => q.QueueName.Equals("email", StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Service for queue of type 'email' not found.");
    }

    public async Task NotifyAboutErrorAsync<T>(Result<T> result, string subject)
    {
        await _emailTaskQueue.QueueBackgroundWorkItem((sp, _) =>
        {
            var emailService = sp.GetRequiredService<IEmailService>();
            return emailService.ComposeAndSendAsync(result, subject);
        });
    }

    public async Task NotifyAboutErrorAsync(string errorMessage, Exception exception, string subject)
    {
        await _emailTaskQueue.QueueBackgroundWorkItem((sp, _) =>
        {
            var emailService = sp.GetRequiredService<IEmailService>();
            return emailService.ComposeAndSendAsync(errorMessage, exception, subject);
        });
    }

    public async Task NotifyAboutErrorAsync(string errorMessage, string subject)
    {
        await _emailTaskQueue.QueueBackgroundWorkItem((sp, _) =>
        {
            var emailService = sp.GetRequiredService<IEmailService>();
            return emailService.ComposeAndSendAsync(errorMessage, subject);
        });
    }
}
