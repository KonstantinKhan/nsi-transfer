using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using Ascon.Polynom.Web.Api.Data.Responses;
using NsiTransfer.Contract.Models.Ascon;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;

namespace NsiTransfer.BLL.Interfaces.Services;

public interface IPolynomApiService
{
    Task<Result<PaginatedList<PropertySearchResultObject>>> GetDiffsInTimePeriod(DateTime start, DateTime end, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<Result<PaginatedList<PropertySearchResultObject>>> ConcreteTimeSearch(DateTime time, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default);
    Task<Result<PaginatedList<PropertySearchResultObject>>> SimpleSearchByName(string substring, int pageNumber, int pageSize = 100, CancellationToken cancellationToken = default);

    Task<Result<ClassificationTreeNode>> GetClassificationRootNode(CancellationToken cancellationToken);
    Task<Result<List<ClassificationTreeNode>>> GetClassificationChildrenNodes(ClassificationTreeNode rootNode, CancellationToken cancellationToken);
    Task<Result<PropertyOwnerResponseCustom>> GetAllPropertiesOfObject(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);

    Task<Result<List<IClassificationObject>>> GetParentGroups(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);
    Task<Result<List<PolynomObjectWithShortProperties>>> GetParentGroupsWithProperties(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);
    Task<Result<bool>> IsGroupLeafAsync(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);
    Task<Result<string?>> GetLastClassificationCodeInGroup(int groupObjectId, IdentifiableObjectType groupTypeId, string minValue, string maxValue, CancellationToken cancellationToken);
    Task<Result<SetPropertyValuesResponse>> UpdateClassificationCodeAsync(
        PolynomObjectWithShortProperties updatedModel,
        PolynomContractWithShortProperties editedContract,
        PolynomShortProperty editedPropertyInContract,
        CancellationToken cancellationToken);
}
