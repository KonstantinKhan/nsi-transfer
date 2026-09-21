using Ascon.Polynom.Web.Api.Data.Models.Search;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;
using NsiTransfer.DAL.Db.Entities;

namespace NsiTransfer.BLL.Interfaces.Services;

/// <summary>
/// Обрабатывает логику установки/вычисления кода классификатора для одного объекта Полином.
/// При успешном выполнении возвращает PolynomObject-лог для сохранения в БД.
/// При неудаче сохраняет PolynomObjectFailure в БД и возвращает ошибку в Result.
/// </summary>
public interface IClassificationCodeProcessor
{
    Task<Result<DAL.Db.Entities.PolynomObject>> ProcessAsync(
        PolynomObjectWithShortProperties mappedInOutputModel,
        Message currentMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Полная переиндексация персистентного кеша последних кодов классификатора (<see cref="DAL.Db.Entities.ClassificationGroupCodeMax"/>)
    /// для всех групп целевого справочника (TargetReferenceNode). Обходит иерархию групп через Polynom API один раз,
    /// для каждой конечной группы с настроенными Min/Max свойствами вычисляет последний выданный код и сохраняет в БД.
    /// Долгая операция (может занимать десятки минут на больших справочниках) — предназначена для запуска вручную,
    /// не как часть обычного sync run.
    /// </summary>
    Task<Result<GroupCodeCacheRebuildResult>> RebuildGroupCodeCacheAsync(CancellationToken cancellationToken);
}
