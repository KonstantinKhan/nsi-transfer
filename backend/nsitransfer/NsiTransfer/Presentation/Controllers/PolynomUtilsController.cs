using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NsiTransfer.BLL.Interfaces.UseCases;
using Swashbuckle.AspNetCore.Annotations;

namespace NsiTransfer.Presentation.Controllers;

[Route("api/polynom-utils")]
[ApiController]
[SwaggerTag("Методы-утилиты, использующие API Полинома напрямую")]
public class PolynomUtilsController : Controller
{
    private readonly IPolynomUtilsUseCases _utilsUseCases;
    private readonly ILogger<PolynomUtilsController> _logger;

    public PolynomUtilsController(IPolynomUtilsUseCases utilsUseCases, ILogger<PolynomUtilsController> logger)
    {
        _utilsUseCases = utilsUseCases;
        _logger = logger;
    }

    /// <summary>
    /// Получить изменения в заданном периоде времени
    /// </summary>
    /// <param name="start">Начало периода (DateTime в формате ISO 8601, example: 2026-04-30T12:00:00Z)</param>
    /// <param name="end">Конец периода (DateTime в формате ISO 8601, example: 2026-04-30T12:00:00Z)</param>
    /// <param name="pageNumber">Номер страницы результатов</param>
    /// <param name="pageSize">Размер страницы результатов (по умолчанию 100)</param>
    [HttpGet("modified-in-time-period")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetModifiedInTimePeriod))]
    public async Task<IActionResult> GetModifiedInTimePeriod([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] int pageNumber, [FromQuery] int pageSize = 100)
    {
        if (start >= end)
        {
            _logger.LogWarning("Некорректные параметры времени: from={From} >= to={To}", start, end);
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Некорректный период",
                Detail = "Начало периода (start) должно быть раньше конца периода (end)",
                Instance = HttpContext.Request.Path
            });
        }

        _logger.LogInformation("Получение изменений в периоде от {From} до {To}, pageSize={PageSize}", start, end, pageSize);

        var result = await _utilsUseCases.FindObjectsModifiedInTimePeriod(start, end, pageNumber, pageSize, HttpContext.RequestAborted);

        return result.IsSuccess
            ? Ok(result.Data)
            : StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ошибка получения изменений",
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
    }

    /// <summary>
    /// Получить данные свойств на конкретный момент времени
    /// </summary>
    /// <param name="time">Момент времени для поиска (DateTime в формате ISO 8601, example: 2026-04-30T12:00:00Z)</param>
    /// <param name="pageNumber">Номер страницы результатов</param>
    /// <param name="pageSize">Размер страницы результатов (по умолчанию 100)</param>
    [HttpGet("modified-in-concrete-time")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetModifiedInConcreteTime))]
    public async Task<IActionResult> GetModifiedInConcreteTime([FromQuery] DateTime time, [FromQuery] int pageNumber, [FromQuery] int pageSize = 100)
    {
        if (time == default)
        {
            _logger.LogWarning("Параметр value не установлен");
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Отсутствует обязательный параметр",
                Detail = "Параметр value (момент времени) обязателен",
                Instance = HttpContext.Request.Path
            });
        }

        _logger.LogInformation("Поиск данных на момент времени {Value}, pageSize={PageSize}", time, pageSize);

        var result = await _utilsUseCases.FindObjectsConcreteTime(time, pageNumber, pageSize, HttpContext.RequestAborted);

        return result.IsSuccess
            ? Ok(result.Data)
            : StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ошибка поиска данных",
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
    }

    /// <summary>
    /// Выполнить простой поиск по названию
    /// </summary>
    /// <param name="substring">Подстрока для поиска в названии</param>
    /// <param name="pageNumber">Номер страницы результатов</param>
    /// <param name="pageSize">Размер страницы результатов (по умолчанию 100)</param>
    [HttpGet("name-contains-substring")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetNameContainsSubstring))]
    public async Task<IActionResult> GetNameContainsSubstring([FromQuery] string substring, [FromQuery] int pageNumber, [FromQuery] int pageSize = 100)
    {
        if (string.IsNullOrWhiteSpace(substring))
        {
            _logger.LogWarning("Параметр name пуст");
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Отсутствует обязательный параметр",
                Detail = "Параметр name (подстрока для поиска) обязателен",
                Instance = HttpContext.Request.Path
            });
        }

        _logger.LogInformation("Простой поиск по названию: {Name}, pageSize={PageSize}", substring, pageSize);

        var result = await _utilsUseCases.FindObjectsByName(substring, pageNumber, pageSize, HttpContext.RequestAborted);

        return result.IsSuccess
            ? Ok(result.Data)
            : StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ошибка выполнения поиска",
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
    }
}
