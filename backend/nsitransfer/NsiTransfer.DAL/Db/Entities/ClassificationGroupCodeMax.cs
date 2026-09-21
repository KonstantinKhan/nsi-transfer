using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;

namespace NsiTransfer.DAL.Db.Entities;

/// <summary>
/// Персистентный кеш последнего выданного кода классификатора для группы.
/// Позволяет не обходить всех дочерних объектов группы через Polynom API при вычислении
/// кода для нового объекта — если строка для группы уже есть, используем её значение как отправную точку.
/// LastMaxCode == null означает, что в группе ещё нет ни одного объекта с кодом (новый код стартует с MinValue группы).
/// </summary>
public class ClassificationGroupCodeMax
{
    public long Id { get; set; }
    public int GroupObjectId { get; set; }
    public IdentifiableObjectType GroupTypeId { get; set; }
    public string? LastMaxCode { get; set; }
    public DateTime UpdatedAt { get; set; }
}
