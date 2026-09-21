using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Classification;
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

    /// <summary>
    /// Возвращает подгруппы группы (element-group/get-by-group) — та же семантика, что использует
    /// IsGroupLeafAsync (пустой список = конечная группа), но с полным списком подгрупп вместо bool.
    /// Нужно для рекурсивного обхода иерархии групп (переиндексация кеша кодов) — в отличие от
    /// GetClassificationChildrenNodes, этот вызов НЕ возвращает элементы/объекты группы, только подгруппы.
    /// </summary>
    Task<Result<List<ElementGroup>>> GetSubGroups(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);

    /// <summary>
    /// Иерархия классификатора: Справочник (Reference) → Каталог (Catalog) → Группа (Group, может содержать
    /// подгруппы или элементы). Возвращает каталоги справочника — верхний уровень под Reference.
    /// </summary>
    Task<Result<List<ElementCatalog>>> GetCatalogsByReference(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает группы верхнего уровня внутри каталога (Catalog → Group) — следующий уровень после
    /// GetCatalogsByReference. Дальше вглубь — GetSubGroups (Group → Group).
    /// </summary>
    Task<Result<List<ElementGroup>>> GetGroupsByCatalog(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken);
    Task<Result<string?>> GetLastClassificationCodeInGroup(int groupObjectId, IdentifiableObjectType groupTypeId, string minValue, string maxValue, CancellationToken cancellationToken);

    /// <summary>
    /// Полный обход дочерних объектов группы — возвращает ВСЕ занятые коды классификатора в диапазоне [minValue, maxValue].
    /// Дорогая операция (полный обход, как GetLastClassificationCodeInGroup) — используется только при поиске
    /// свободного номера после исчерпания диапазона группы (объекты были удалены из середины).
    /// </summary>
    Task<Result<HashSet<string>>> GetAllClassificationCodesInGroup(int groupObjectId, IdentifiableObjectType groupTypeId, string minValue, string maxValue, CancellationToken cancellationToken);
    Task<Result<SetPropertyValuesResponse>> UpdateClassificationCodeAsync(
        PolynomObjectWithShortProperties updatedModel,
        PolynomContractWithShortProperties editedContract,
        PolynomShortProperty editedPropertyInContract,
        CancellationToken cancellationToken);
}
