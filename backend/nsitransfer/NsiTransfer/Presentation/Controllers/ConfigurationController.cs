using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Exceptions;
using NsiTransfer.Contract.Interfaces;
using Swashbuckle.AspNetCore.Annotations;

namespace NsiTransfer.Presentation.Controllers;

/// <summary>
/// Контроллер для управления конфигурацией приложения
/// </summary>
[ApiController]
[Route("api/configuration")]
[Authorize]
public class ConfigurationController : ControllerBase
{
    private readonly IOptionsHelper<AppConfiguration> _optionsHelper;
    private readonly IConfigurationWriter _writer;
    private readonly ISyncUseCases _syncUseCases;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IOptionsHelper<AppConfiguration> optionsHelper,
        IConfigurationWriter writer,
        ISyncUseCases syncUseCases,
        ILogger<ConfigurationController> logger)
    {
        _optionsHelper = optionsHelper;
        _writer = writer;
        _syncUseCases = syncUseCases;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetConfiguration))]
    public Task<IActionResult> GetConfiguration()
    {
        if (_optionsHelper.TryGetCurrentValue(out var config))
        {
            return Task.FromResult<IActionResult>(Ok(config));
        }

        return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Конфигурация недоступна",
            Detail = "Файл конфигурации на диске содержит невалидные данные.",
            Instance = HttpContext.Request.Path
        }));
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(ReplaceConfiguration))]
    public async Task<IActionResult> ReplaceConfiguration([FromBody] AppConfiguration updated, CancellationToken ct)
    {
        return await ExecuteWriteAsync(() => _writer.ReplaceAsync(updated, ct), nameof(ReplaceConfiguration));
    }

    [HttpGet("target-reference-node")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetTargetReferenceNode))]
    public Task<IActionResult> GetTargetReferenceNode()
    {
        return GetSectionAsync(c => c.TargetReferenceNode);
    }

    [HttpPut("target-reference-node")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(UpdateTargetReferenceNode))]
    public async Task<IActionResult> UpdateTargetReferenceNode([FromBody] TargetReferenceNode updated, CancellationToken ct)
    {
        return await ExecuteWriteAsync(() => _writer.UpdateAsync(c => c.TargetReferenceNode = updated, ct), nameof(UpdateTargetReferenceNode));
    }

    [HttpGet("rabbitmq-queues")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetRabbitMqQueues))]
    public Task<IActionResult> GetRabbitMqQueues()
    {
        return GetSectionAsync(c => c.RabbitMqQueues);
    }

    [HttpPut("rabbitmq-queues")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(UpdateRabbitMqQueues))]
    public async Task<IActionResult> UpdateRabbitMqQueues([FromBody] RabbitMqQueues updated, CancellationToken ct)
    {
        return await ExecuteWriteAsync(() => _writer.UpdateAsync(c => c.RabbitMqQueues = updated, ct), nameof(UpdateRabbitMqQueues));
    }

    [HttpGet("rabbitmq-retry-params")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetRabbitMqRetryParams))]
    public Task<IActionResult> GetRabbitMqRetryParams()
    {
        return GetSectionAsync(c => c.RabbitMqRetryParams);
    }

    [HttpPut("rabbitmq-retry-params")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(UpdateRabbitMqRetryParams))]
    public async Task<IActionResult> UpdateRabbitMqRetryParams([FromBody] RabbitMqRetryParams updated, CancellationToken ct)
    {
        return await ExecuteWriteAsync(() => _writer.UpdateAsync(c => c.RabbitMqRetryParams = updated, ct), nameof(UpdateRabbitMqRetryParams));
    }

    [HttpGet("polynom-api-sync-options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetPolynomApiSyncOptions))]
    public Task<IActionResult> GetPolynomApiSyncOptions()
    {
        return GetSectionAsync(c => c.PolynomApiSyncOptions);
    }

    [HttpPut("polynom-api-sync-options")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(UpdatePolynomApiSyncOptions))]
    public async Task<IActionResult> UpdatePolynomApiSyncOptions([FromBody] PolynomApiSyncOptions updated, CancellationToken ct)
    {
        return await ExecuteWriteAsync(() => _writer.UpdateAsync(c => c.PolynomApiSyncOptions = updated, ct), nameof(UpdatePolynomApiSyncOptions));
    }

    [HttpGet("email-notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetEmailNotifications))]
    public Task<IActionResult> GetEmailNotifications()
    {
        return GetSectionAsync(c => c.EmailNotifications);
    }

    [HttpPut("email-notifications")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(UpdateEmailNotifications))]
    public async Task<IActionResult> UpdateEmailNotifications([FromBody] EmailNotificationsOptions updated, CancellationToken ct)
    {
        return await ExecuteWriteAsync(() => _writer.UpdateAsync(c => c.EmailNotifications = updated, ct), nameof(UpdateEmailNotifications));
    }

    private Task<IActionResult> GetSectionAsync<TSection>(Func<AppConfiguration, TSection> selector)
    {
        // Неблокирующее чтение (lock-free)
        if (_optionsHelper.TryGetCurrentValue(out var config))
        {
            return Task.FromResult<IActionResult>(Ok(selector(config!)));
        }

        return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Конфигурация недоступна",
            Detail = "Файл конфигурации на диске содержит невалидные данные.",
            Instance = HttpContext.Request.Path
        }));
    }

    private async Task<IActionResult> ExecuteWriteAsync(Func<Task> write, string operationName)
    {
        var activeSyncCheck = await _syncUseCases.IsThereAlreadyActiveSync(HttpContext.RequestAborted);
        if (!activeSyncCheck.IsSuccess)
        {
            _logger.LogWarning("Попытка изменить конфигурацию во время активной синхронизации. Операция: {Operation}", operationName);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Невозможно изменить конфигурацию во время активной синхронизации",
                Detail = activeSyncCheck.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            await write();
            return NoContent();
        }
        catch (ConfigurationValidationException ex)
        {
            _logger.LogWarning("Ошибка валидации конфигурации при операции {Operation}: {Errors}", operationName, ex.Failures);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ошибка валидации конфигурации",
                Detail = string.Join("; ", ex.Failures),
                Instance = HttpContext.Request.Path
            };

            problemDetails.Extensions["errors"] = new Dictionary<string, string[]>
            {
                [nameof(AppConfiguration)] = ex.Failures.ToArray()
            };

            return BadRequest(problemDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении операции {Operation}", operationName);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ошибка выполнения операции",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }
}