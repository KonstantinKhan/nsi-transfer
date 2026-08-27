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
}
