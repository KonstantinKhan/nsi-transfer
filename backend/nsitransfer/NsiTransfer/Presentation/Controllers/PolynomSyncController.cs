using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.BLL.Tools;
using NsiTransfer.Contract.Models;
using NsiTransfer.Contract.Models.DTO;
using NsiTransfer.Contract.Models.EventArgs;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

namespace NsiTransfer.Presentation.Controllers;

[Route("api/polynom-sync")]
[ApiController]
[SwaggerTag("Методы для работы с функционалом синхронизации состояния объектов из Полинома во внешнюю систему")]
public class PolynomSyncController : ControllerBase
{
    private readonly ISyncUseCases _syncUseCases;
    private readonly ISyncNotifier _syncNotifier;
    private readonly ILogger<PolynomSyncController> _logger;

    public PolynomSyncController(
        ISyncUseCases syncUseCases,
        ISyncNotifier notifier,
        ILogger<PolynomSyncController> logger)
    {
        _syncUseCases = syncUseCases;
        _syncNotifier = notifier;
        _logger = logger;
    }



    /// <summary>
    /// Запустить процесс сбора и синхронизации данных из Полинома
    /// </summary>
    /// <remarks>
    /// Метод инициирует фоновый процесс, который:
    /// <list type="number">
    ///   <item><description>Определяет время последней успешной синхронизации</description></item>
    ///   <item><description>Собирает изменения объектов за период с момента последней синхронизации до текущего времени</description></item>
    ///   <item><description>Сохраняет собранные данные и публикует их в очередь RabbitMQ</description></item>
    /// </list>
    /// Временной диапазон рассчитывается автоматически на основе истории запусков.
    /// </remarks>
    [HttpPost("start-data-collection-and-wait")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(StartDataCollectionAndWaitAsync))]
    public async Task<IActionResult> StartDataCollectionAndWaitAsync([FromBody] StartDataCollectionRequest request)
    {
        _logger.LogInformation("Получен аутентифицированный запрос на запуск синхронизации данных из Полинома с ожиданием ответа");
        return await StartDataCollectionInternalAsync(request, false);
    }

    /// <summary>
    /// Запустить процесс сбора и синхронизации данных из Полинома (для внешних триггеров)
    /// </summary>
    /// <remarks>
    /// Метод инициирует фоновый процесс, который:
    /// <list type="number">
    ///   <item><description>Определяет время последней успешной синхронизации</description></item>
    ///   <item><description>Собирает изменения объектов за период с момента последней синхронизации до текущего времени</description></item>
    ///   <item><description>Сохраняет собранные данные и публикует их в очередь RabbitMQ</description></item>
    /// </list>
    /// Временной диапазон рассчитывается автоматически на основе истории запусков.
    /// Этот эндпоинт не требует аутентификации и использует сервисный аккаунт для доступа к API Полинома.
    /// </remarks>
    [HttpPost("autooperation/start-data-collection-and-wait")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(StartDataCollectionFromExternalTriggerAsync))]
    public async Task<IActionResult> StartDataCollectionFromExternalTriggerAsync([FromBody] StartDataCollectionRequest request)
    {
        _logger.LogInformation("Получен неаутентифицированный запрос на запуск синхронизации данных из Полинома с ожиданием ответа. Инициатор: {initiator}", request.initiatorName);
        return await StartDataCollectionInternalAsync(request, true);
    }

    private async Task<IActionResult> StartDataCollectionInternalAsync(StartDataCollectionRequest request, bool isAutooperation)
    {
        var hasActiveSync = await _syncUseCases.IsThereAlreadyActiveSync(HttpContext.RequestAborted);

        if (!hasActiveSync.IsSuccess)
        {
            var triggerSource = isAutooperation ? " (автооперация)" : "";
            _logger.LogWarning("Попытка запуска синхронизации во время выполнения другой синхронизации{TriggerSource}", triggerSource);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Попытка запуска синхронизации во время выполнения другой синхронизации",
                Detail = hasActiveSync.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        var (result, sendingModel) = await _syncUseCases.StartDataCollectionAndWaitAsync(request.initiatorName, HttpContext.RequestAborted);

        if (result.IsSuccess)
        {
            var triggerSource = isAutooperation ? " (автооперация)" : "";
            _logger.LogInformation("Синхронизация данных успешно завершена{TriggerSource}", triggerSource);
            return Ok(sendingModel);
        }

        var triggerSourceError = isAutooperation ? " (автооперация)" : "";
        _logger.LogError("Ошибка при синхронизации данных{TriggerSource}: {ErrorMessage}", triggerSourceError, result.ErrorMessage);
        var serializedSendingModel = sendingModel != null ? JsonSerializationHelper.SerializeToJsonSimple(sendingModel, true) : "null";
        return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ошибка синхронизации данных",
            Detail = $"ErrorMessage: {result.ErrorMessage}\n SendingModel: {serializedSendingModel}",
            Instance = HttpContext.Request.Path
        });
    }

    [HttpPost("start-data-collection-in-background")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(StartDataCollectionInBackgroundAsync))]
    public async Task<IActionResult> StartDataCollectionInBackgroundAsync([FromBody] StartDataCollectionRequest request)
    {
        _logger.LogInformation("Получен запрос на запуск фоновой синхронизации данных из Полинома");

        var hasActiveSync = await _syncUseCases.IsThereAlreadyActiveSync(HttpContext.RequestAborted);

        if (!hasActiveSync.IsSuccess)
        {
            _logger.LogWarning("Попытка запуска фоновой синхронизации во время выполнения другой синхронизации");
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Попытка запуска фоновой синхронизации во время выполнения другой синхронизации",
                Detail = hasActiveSync.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        var (result, sendingModel) = await _syncUseCases.StartDataCollectionInBackgroundAsync(request.initiatorName, HttpContext.RequestAborted);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Фоновый процесс синхронизации данных успешно запущен");
            return Ok(sendingModel);
        }

        _logger.LogError("Ошибка при запуске фонового процесса синхронизации данных из Полинома: {ErrorMessage}", result.ErrorMessage);

        return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ошибка запуска фонового процесса синхронизации данных из Полинома",
            Detail = result.ErrorMessage,
            Instance = HttpContext.Request.Path
        });
    }


    [HttpGet("listen-for-all-sync-events")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task ListenForNewSendings()
    {
        var sseChannel = Channel.CreateUnbounded<SyncEventEventArgs>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        void OnSyncEvent(object sender, SyncEventEventArgs e)
        {
            sseChannel.Writer.TryWrite(e);
        }

        _syncNotifier.SyncEventsEvent += OnSyncEvent;

        try
        {
            // Заголовки устанавливаем ОДИН РАЗ, ДО начала записи тела
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache, no-transform");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Для Nginx

            await Response.StartAsync(HttpContext.RequestAborted);

            // Читаем события из канала и отправляем их клиенту
            await foreach (var sendingSyncEvent in sseChannel.Reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                if (HttpContext.RequestAborted.IsCancellationRequested) break;

                var eventType = sendingSyncEvent.SyncEvent.EventType;

                // Обработка heartbeat: если событие начинается с ":", отправляем его как комментарий SSE
                if (eventType.StartsWith(":"))
                {
                    await Response.WriteAsync($"{eventType}\n\n", HttpContext.RequestAborted);
                }
                else
                {
                    var payload = sendingSyncEvent.SyncEvent.Payload;
                    var dto = new SendingSyncEventDto(sendingSyncEvent.SendingId, eventType, payload);
                    var json = JsonSerializationHelper.SerializeToJsonSimple(dto, false);

                    await Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", HttpContext.RequestAborted);
                }

                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE stream error for all sendings");

            try
            {
                await Response.WriteAsync($"event: Error\ndata: {{\"message\": \"Internal server error\"}}\n\n", HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
            catch
            {
                // Соединение уже мертво, игнорируем
            }
        }
        finally
        {
            _syncNotifier.SyncEventsEvent -= OnSyncEvent;
            sseChannel.Writer.TryComplete();
        }
    }


    [HttpGet("listen-for-sync-events")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task ListenForSyncEvents(Guid sendingId)
    {
        if (!_syncNotifier.TryGetReader(sendingId, out var reader))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        try
        {
            // Заголовки устанавливаем ОДИН РАЗ, ДО начала записи тела
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache, no-transform");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // Для Nginx

            // Явно начинаем ответ (фиксируем заголовки и начинаем chunked encoding)
            await Response.StartAsync(HttpContext.RequestAborted);
            
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                var readTask = reader.WaitToReadAsync(HttpContext.RequestAborted).AsTask();

                var completed = await Task.WhenAny(
                    readTask,
                    Task.Delay(TimeSpan.FromSeconds(15), HttpContext.RequestAborted));

                if (completed != readTask)
                {
                    await Response.WriteAsync(": heartbeat\n\n", HttpContext.RequestAborted);
                    await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    continue;
                }

                if (!await readTask)
                {
                    await Response.WriteAsync("event: Closed\ndata: {\"message\": \"Stream completed\"}\n\n", HttpContext.RequestAborted);
                    await Response.Body.FlushAsync(HttpContext.RequestAborted);
                    break;
                }

                // Читаем все доступные события
                while (reader.TryRead(out var syncEvent))
                {
                    var json = JsonSerializationHelper.SerializeToJsonSimple(syncEvent.Payload, false);

                    await Response.WriteAsync($"event: {syncEvent.EventType}\ndata: {json}\n\n", HttpContext.RequestAborted);
                }

                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE stream error for sending {SendingId}", sendingId);

            // Пытаемся отправить событие об ошибке клиенту
            try
            {
                await Response.WriteAsync(
                    $"event: Error\ndata: {{\"message\": \"Internal server error\"}}\n\n",
                    HttpContext.RequestAborted);
                await Response.Body.FlushAsync(HttpContext.RequestAborted);
            }
            catch
            {
                // Соединение уже мертво, игнорируем
            }
        }
    }

    [HttpGet("sendings")]
    [ProducesResponseType(typeof(IReadOnlyList<SendingModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<IReadOnlyList<SendingModel>>> GetSendings(
        [FromQuery, Range(1, 200)] int pageSize = 30,
        [FromQuery] DateTime? cursorInitiatedAt = null,
        [FromQuery] Guid? cursorId = null)
    {
        try
        {
            if (cursorInitiatedAt.HasValue != cursorId.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Некорректный курсор пагинации",
                    Detail = "cursorInitiatedAt и cursorId должны передаваться только вместе.",
                    Instance = HttpContext.Request.Path
                });
            }
            
            var cursor = cursorInitiatedAt.HasValue
                ? new SendingsCursor(cursorInitiatedAt.Value, cursorId!.Value)
                : (SendingsCursor?)null;

            var page = await _syncUseCases.GetSendingsPageAsync(pageSize, cursor, HttpContext.RequestAborted);

            return Ok(new
            {
                page.Items,
                page.HasMore
            }); 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении списка синхронизаций");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ошибка при получении списка отправлений",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }

    [HttpGet("sending")]
    [ProducesResponseType(typeof(SendingModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<SendingModel>> GetSending([FromQuery] string sendingId)
    {
        try
        {
            if (!Guid.TryParse(sendingId, out var sendingIdGuid))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Не корректный sendingId, не удаётся преобразовать в Guid",
                    Detail = string.Empty,
                    Instance = HttpContext.Request.Path
                });
            }

            var sending = await _syncUseCases.GetSending(sendingIdGuid, HttpContext.RequestAborted);
            return Ok(sending);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении отправления с SendingId {sendingId}", sendingId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = $"Ошибка при получении отправления с SendingId {sendingId}",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }
}

public record SendingSyncEventDto(Guid SendingId, string EventType, object? Payload);

public record StartDataCollectionRequest(string initiatorName);