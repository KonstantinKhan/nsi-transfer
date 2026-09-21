using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models.DTO;

public class PolynomObjectWithShortProperties : PolynomObjectWithName
{
    public List<PolynomContractWithShortProperties> Contracts { get; set; }

    /// <summary>
    /// Группа классификатора, в которой хранится объект. Заполняется в ClassificationCodeProcessor
    /// (при вычислении/проверке кода классификатора уже определена корректная родительская группа объекта —
    /// используем её без дополнительных запросов к Polynom API). Null, если группа не была однозначно определена
    /// (например, объект не относится к понятию "Данные Классификатора" или у него нет родительских групп).
    /// </summary>
    public GroupInfo? GroupInfo { get; set; }
}
