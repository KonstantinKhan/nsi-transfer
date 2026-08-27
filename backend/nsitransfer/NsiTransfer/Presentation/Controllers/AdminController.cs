using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NsiTransfer.BLL.Interfaces.UseCases;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Presentation.Controllers;

[Route("api/admin-panel")]
[ApiController]
[SwaggerTag("Методы для настройки синхронизации со справочниками")]
public class AdminController : ControllerBase
{
    private readonly IAdminPanelUseCases _useCases;

    public AdminController(IAdminPanelUseCases useCases)
    {
        _useCases = useCases;
    }

    /// <summary>
    /// Получить узлы первого уровня классификации из Полинома
    /// </summary>
    /// <remarks>
    /// Возвращает список дочерних узлов корневого справочника классификации.
    /// Используется для выбора целевого справочника синхронизации.
    /// </remarks>
    /// <response code="200">Список узлов классификации</response>
    /// <response code="400">Ошибка при получении данных из внешнего API</response>
    /// <response code="500">Внутренняя ошибка сервера</response>
    [HttpGet("reference/first-layer")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ClassificationTreeNode>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    [SwaggerOperation(OperationId = nameof(GetReferenceFirstLayerNodes))]
    public async Task<IActionResult> GetReferenceFirstLayerNodes()
    {
        var result = await _useCases.GetReferenceFirstLayerNodes(HttpContext.RequestAborted);

        if (!result.IsSuccess)
        {
            // Возвращаем 400, т.к. ошибка обычно связана с внешним API или данными
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ошибка получения классификации",
                Detail = result.ErrorMessage,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Установить узел классификации как целевой для синхронизации
    /// </summary>
    /// <remarks>
    /// Активирует выбранный справочник как источник данных для синхронизации.
    /// Все предыдущие активные конфигурации будут деактивированы.
    /// </remarks>
    /// <param name="request">Данные узла классификации</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <response code="204">Конфигурация успешно обновлена</response>
    /// <response code="400">Некорректные входные данные или ошибка валидации</response>
    /// <response code="500">Ошибка при сохранении конфигурации</response>
    //[HttpPost("reference/target-node")]
    //[Authorize]
    //[ProducesResponseType(StatusCodes.Status204NoContent)]
    //[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    //[ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    //[SwaggerOperation(OperationId = nameof(SetReferenceTargetNode))]
    //public async Task<IActionResult> SetReferenceTargetNode(
    //    [FromBody][Required] SetTargetNodeRequest request,
    //    CancellationToken cancellationToken = default)
    //{
    //    AccessControlObject? nodeObject = null;
    //    try
    //    {
    //        nodeObject = new AccessControlObject
    //        {
    //            ObjectId = request.NodeObjectId,
    //            TypeId = (IdentifiableObjectType)request.NodeTypeId
    //        };
    //    }
    //    catch (Exception)
    //    {
    //        return BadRequest($"Передано неверное значение {nameof(request.NodeTypeId)}, такого TypeId не существует");
    //    }

    //    var result = await _useCases.SetReferenceNodeAsTargetNodeForSync(request.NodeName, request.NodeObjectId, request.NodeTypeId);

    //    if (!result.IsSuccess)
    //    {
    //        return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
    //        {
    //            Status = StatusCodes.Status500InternalServerError,
    //            Title = "Ошибка сохранения конфигурации",
    //            Detail = result.ErrorMessage,
    //            Instance = HttpContext.Request.Path
    //        });
    //    }

    //    // 204 No Content — стандарт для успешного обновления без возврата данных
    //    return NoContent();
    //}
}

/// <summary>
/// DTO для установки целевого узла классификации
/// </summary>
public class SetTargetNodeRequest
{
    /// <summary>
    /// Идентификатор объекта узла классификации
    /// </summary>
    /// <example>12345</example>
    [Required(ErrorMessage = "NodeObjectId обязателен")]
    public int NodeObjectId { get; set; }

    /// <summary>
    /// Тип объекта узла классификации (числовое значение перечисления)
    /// </summary>
    /// <example>2</example>
    [Required(ErrorMessage = "NodeTypeId обязателен")]
    public int NodeTypeId { get; set; }

    public string NodeName { get; set; }
}