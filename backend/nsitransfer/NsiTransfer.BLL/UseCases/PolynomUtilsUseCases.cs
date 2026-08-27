using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Interfaces.UseCases;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.UseCases;

public class PolynomUtilsUseCases : IPolynomUtilsUseCases
{
    private readonly IPolynomApiService _apiService;

    public PolynomUtilsUseCases(IPolynomApiService apiService)
    {
        _apiService = apiService;
    }

    public Task<Result<PaginatedList<PropertySearchResultObject>>> FindObjectsModifiedInTimePeriod(DateTime start, DateTime end, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default)
        => _apiService.GetDiffsInTimePeriod(start, end, pageNumber, pageSize, cancellationToken);

    public Task<Result<PaginatedList<PropertySearchResultObject>>> FindObjectsConcreteTime(DateTime time, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default)
        => _apiService.ConcreteTimeSearch(time, pageNumber, pageSize, cancellationToken);

    public Task<Result<PaginatedList<PropertySearchResultObject>>> FindObjectsByName(string substring, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default)
        => _apiService.SimpleSearchByName(substring, pageNumber, pageSize, cancellationToken);
}
