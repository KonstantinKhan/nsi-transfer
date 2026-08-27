using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.PropertyOwner;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Tree;
using Ascon.Polynom.Web.Api.Data.Requests.Search;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Interfaces.Services;

public interface IPolynomRequestBuilder
{
    Task<Result<PropertySearchRequest>> TimePeriodRequest_Test(DateTime from, DateTime to, int pageSize = 100);
    Task<Result<PropertySearchRequest>> TimePeriodRequest(DateTime from, DateTime to, int pageSize = 100, int pageNumber = 1);
    Task<Result<PropertySearchRequest>> ConcreteTimeSearchRequest(DateTime time, int pageSize = 100, int pageNumber = 1);
    Task<Result<PropertySearchRequest>> NameContainsSubstringSearchRequest(string substring, int pageSize = 10, int pageNumber = 1);


    IClassificationTreeRequest ClassificationRootNodeRequest();
    IClassificationNodeChildrenRequest ClassificationChildrenNodesRequest(IAccessControlObject nodeObject,
        TreeFilterOptions filterOptions,
        string? filterString,
        ClassificationTreeOptions options = ClassificationTreeOptions.Default, 
        int pageNumber = 1);
    IGetPropertiesRequest GetPropertiesRequest(int objectId, IdentifiableObjectType typeId);
}
