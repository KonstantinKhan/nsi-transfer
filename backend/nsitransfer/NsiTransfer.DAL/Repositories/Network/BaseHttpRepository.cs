using Microsoft.Extensions.Logging;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.DAL.Repositories.Network;

internal abstract class BaseHttpRepository(ILogger logger)
{
    protected ILogger Logger => logger;

    protected static async Task<Result> CreateHttpErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase) ? "Unknown" : response.ReasonPhrase;
        return $"HTTP status code {statusCode}; {reason}; {errorContent}";
    }

    protected static async Task<Result<T>> CreateHttpErrorAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var statusCode = (int)response.StatusCode;
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase) ? "Unknown" : response.ReasonPhrase;
        return $"HTTP status code {statusCode}; {reason}; {errorContent}";
    }

    protected async Task<Result<HttpResponseMessage>> SendRequestAsync(Func<Task<HttpResponseMessage>> sendFunc, CancellationToken cancellationToken)
    {
        try
        {
            var response = await sendFunc();
            return response;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            var errorMessage = $"Запрос был отменен. Exception: {ex.Message}";
            logger.LogError(ex, errorMessage);
            return errorMessage;
        }
        catch (TaskCanceledException ex)
        {
            var errorMessage = $"Превышено время ожидания запроса. Exception: {ex.Message}";
            logger.LogError(ex, errorMessage);
            return errorMessage;
        }
    }
}