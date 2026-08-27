using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using NsiTransfer.BLL.Tools;
using NsiTransfer.DAL.Interfaces.MessageBrokers;
using System.Net.Mime;
using System.Text.Json;

namespace NsiTransfer.Presentation.Controllers.Examples;

/// <summary>
/// Пример контроллера с использованием RabbitMqPublishingService для отправки сообщений в RabbitMQ
/// Удалите этот контроллер после изучения, это только пример
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RabbitMqExampleController : ControllerBase
{
    private readonly IRabbitMqPublisher _publisher;
    //private readonly RabbitMqMessageStore _messageStore;
    private readonly ILogger<RabbitMqExampleController> _logger;

    public RabbitMqExampleController(
        IRabbitMqPublisher publisher,
        //RabbitMqMessageStore messageStore,
        ILogger<RabbitMqExampleController> logger)
    {
        _publisher = publisher;
        //_messageStore = messageStore;
        _logger = logger;
    }

    /// <summary>
    /// Отправить текстовое сообщение в RabbitMQ
    /// </summary>
    [HttpPost("send-text")]
    [Consumes("text/plain")]
    public async Task<IActionResult> SendTextMessage([FromBody] string message)
    {
        try
        {
            await _publisher.PublishAsync("my-exchange", "my-queue", MediaTypeNames.Text.Plain, "test", message, HttpContext.RequestAborted);
            return Ok(new { success = true, message = "Сообщение отправлено" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Отправить JSON сообщение в RabbitMQ
    /// </summary>
    [HttpPost("send-json")]
    [Consumes("application/json")]
    public async Task<IActionResult> SendJsonMessage([FromBody] object data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data);
            await _publisher.PublishAsync("my-exchange", "my-queue", MediaTypeNames.Application.Json, "test", json, HttpContext.RequestAborted);
            return Ok(new { success = true, message = "JSON сообщение отправлено" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке JSON сообщения");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Отправить сообщение с кастомным типом контента
    /// </summary>
    [HttpPost("send-custom")]
    public async Task<IActionResult> SendCustomMessage(
        [FromQuery] string queueName,
        [FromQuery] string contentType,
        [FromBody] string message)
    {
        try
        {
            await _publisher.PublishAsync("my-exchange", queueName, contentType, "test", message, HttpContext.RequestAborted);
            return Ok(new { success = true, message = "Сообщение отправлено" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке сообщения");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Получить последнее полученное сообщение (если включен RabbitMqListener)
    /// </summary>
    [HttpGet("last-received-message")]
    public IActionResult GetLastReceivedMessage()
    {
        //var result = _messageStore.TryDequeue(out var message);
        //if (!result)
        //    return NotFound(new { message = "Нет полученных сообщений" });

        //return Ok(new { message });

        return Ok();
    }
}
