using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.PropertyOwner;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Tree;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Models.Properties;
using Ascon.Polynom.Web.Api.Data.Models.Properties.Definition;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using Ascon.Polynom.Web.Api.Data.Requests.Base;
using Ascon.Polynom.Web.Api.Data.Requests.Search;
using Ascon.Polynom.Web.Api.Data.Responses;
using NsiTransfer.Contract.Models.Ascon;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.DAL.Interfaces.Http;

public interface IPolynomApiHttpRepository
{
    /// <summary>
    /// Выполняет поиск свойств в Полиноме по заданным критериям.
    /// </summary>
    Task<Result<PaginatedList<PropertySearchResultObject>>> ExecuteSearchProperty(PropertySearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает узлы дерева классификации по корневому запросу.
    /// </summary>
    Task<Result<PaginatedList<ClassificationTreeNode>>> GetClassificationTreeNode(IClassificationTreeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает дочерние узлы дерева классификации для конкретного родителя.
    /// </summary>
    Task<Result<PaginatedList<ClassificationTreeNode>>> GetClassificationNodeChildren(IClassificationNodeChildrenRequest request, CancellationToken cancellationToken = default);

    Task<Result<PropertyDefinition>> GetPropertyDefinition(IGetByAbsoluteCodeRequest request, CancellationToken cancellationToken = default);
    Task<Result<ConceptPropertySource>> GetConceptPropertySource(GetByAbsoluteCodeRequest request, CancellationToken cancellationToken);
    Task<Result<PropertyOwnerResponseCustom>> GetAllPropertiesOfPropertyOwner(IGetPropertiesRequest getPropertiesRequest, CancellationToken cancellationToken);

    Task<Result<List<IClassificationObject>>> GetParentGroups(IIdentifierRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Получает группы, находящиеся внутри указанной группы элементов.
    /// </summary>
    Task<Result<List<ElementGroup>>> GetGroupsInsideElementGroup(IIdentifierRequest request, CancellationToken cancellationToken);

    Task<Result<SetPropertyValuesResponse>> SetPropertyValuesOfPropertyOwner(ISetPropertyValuesRequest request, CancellationToken cancellationToken);
}
