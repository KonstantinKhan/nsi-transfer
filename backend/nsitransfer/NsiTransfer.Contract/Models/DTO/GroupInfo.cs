using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;

namespace NsiTransfer.Contract.Models.DTO;

/// <summary>
/// Информация о группе классификатора, в которой хранится объект — чтобы получатель мог однозначно
/// определить место объекта в справочнике без дополнительных запросов к Polynom API.
/// MinCode/MaxCode == null, если у группы не настроены соответствующие свойства.
/// </summary>
public class GroupInfo
{
    public int ObjectId { get; set; }
    public IdentifiableObjectType TypeId { get; set; }
    public string Name { get; set; }
    public string? MinCode { get; set; }
    public string? MaxCode { get; set; }
    public bool IsLeaf { get; set; }
}
