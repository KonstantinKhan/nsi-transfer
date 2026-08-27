using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Tools;
using System.Net;
using System.Text.Json;

namespace NsiTransfer.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IBackgroundTaskQueue _emailTaskQueue;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IEnumerable<IBackgroundTaskQueue> backQueues,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _emailTaskQueue = backQueues.FirstOrDefault(q => q.QueueName.Equals("email", StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Service for queue of type 'email' not found.");
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!!Произошло необработанное исключение в системе!!!");
            await HandleExceptionAsync(context, ex, DateTime.UtcNow);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, DateTime time)
    {
        var errorMessage = "Произошла внутренняя ошибка сервера.";
        await NotifyAboutError(errorMessage, exception, errorMessage);

        // Если ответ уже начал отправляться (заголовки ушли), мы не можем изменить статус-код
        if (context.Response.HasStarted)
        {
            _logger.LogError(exception, "Невозможно обработать исключение, так как ответ уже начал отправляться.");
            return;
        }
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // Настраиваем детали в зависимости от окружения
        string details = _environment.IsDevelopment()
            ? exception.ToString() // В Dev показываем полный StackTrace
            : $"Неизвестная и необработанная внутренняя ошибка сервера. Точное время: {time:yyyy.MM.dd hh:mm:ss:fffff}"; // В Prod скрываем детали

        var response = new ErrorResponse(
            Message: errorMessage,
            Details: details,
            TraceId: context.TraceIdentifier
        );

        var json = JsonSerializationHelper.SerializeToJsonSimple(response, true);

        await context.Response.WriteAsync(json);
    }

    private async Task NotifyAboutError(string errorMessage, Exception exception, string subject)
    {
        await _emailTaskQueue.QueueBackgroundWorkItem((sp, stopTkn) =>
        {
            var emailService = sp.GetRequiredService<IEmailService>();
            return emailService.ComposeAndSendAsync(errorMessage, exception, subject);
        });
    }
}

public record ErrorResponse(string Message, string? Details = null, string? TraceId = null);
