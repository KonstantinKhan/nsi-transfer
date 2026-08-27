using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Interfaces.UseCases;

public interface IPolynomUtilsUseCases
{
    Task<Result<PaginatedList<PropertySearchResultObject>>> FindObjectsModifiedInTimePeriod(DateTime start, DateTime end, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<Result<PaginatedList<PropertySearchResultObject>>> FindObjectsConcreteTime(DateTime time, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<Result<PaginatedList<PropertySearchResultObject>>> FindObjectsByName(string substring, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default);
}
