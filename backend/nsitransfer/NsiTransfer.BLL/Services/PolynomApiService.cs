using Ascon.Polynom.Web.Api.Data;
using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Properties.Values;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Models.Properties.Values;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using Ascon.Polynom.Web.Api.Data.Requests.Base;
using Ascon.Polynom.Web.Api.Data.Requests.Properties.Values;
using Ascon.Polynom.Web.Api.Data.Requests.PropertyOwner;
using Ascon.Polynom.Web.Api.Data.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql.Internal.Postgres;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Tools;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Ascon;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;
using NsiTransfer.DAL.Interfaces.Http;

namespace NsiTransfer.BLL.Services;

public class PolynomApiService : IPolynomApiService
{
    private readonly IPolynomApiHttpRepository _apiRepository;
    private readonly IPolynomRequestBuilder _requestBuilder;
    private readonly IOptionsMonitor<PolynomApiSyncOptions> _apiOptions;
    private readonly ILogger<PolynomApiService> _logger;

    public PolynomApiService(
        IPolynomApiHttpRepository apiRepository,
        IPolynomRequestBuilder requestBuilder,  
        IOptionsMonitor<PolynomApiSyncOptions> apiOptions,
        ILogger<PolynomApiService> logger)
    {
        _apiRepository = apiRepository;
        _requestBuilder = requestBuilder;
        _apiOptions = apiOptions;
        _logger = logger;
    }

    public Task<Result<PaginatedList<PropertySearchResultObject>>> GetDiffsInTimePeriod(
        DateTime start, DateTime end, 
        int pageNumber, int pageSize = 100, 
        CancellationToken cancellationToken = default)
    {
        var searchResult = _requestBuilder.TimePeriodRequest(start, end, pageSize, pageNumber)
            .BindAsync(req => _apiRepository.ExecuteSearchProperty(req, cancellationToken))
            .OnFailureAsync(err => err.AddError($"Не удалось получить изменения за период с {start:dd.MM.yyyy} по {end:dd.MM.yyyy}."));

        return searchResult;
    }

    public Task<Result<PaginatedList<PropertySearchResultObject>>> ConcreteTimeSearch(
        DateTime time, 
        int pageNumber, int pageSize = 100, 
        CancellationToken cancellationToken = default)
    {
        var searchResult = _requestBuilder.ConcreteTimeSearchRequest(time, pageSize, pageNumber)
            .BindAsync(req => _apiRepository.ExecuteSearchProperty(req, cancellationToken))
            .OnFailureAsync(err => err.AddError($"Не удалось выполнить поиск по конкретному времени {time:dd.MM.yyyy HH:mm:ss}."));

        return searchResult;
    }

    public Task<Result<PaginatedList<PropertySearchResultObject>>> SimpleSearchByName(
        string substring, 
        int pageNumber, int pageSize = 100, 
        CancellationToken cancellationToken = default)
    {
        var searchResult = _requestBuilder.NameContainsSubstringSearchRequest(substring, pageSize, pageNumber)
            .BindAsync(req => _apiRepository.ExecuteSearchProperty(req, cancellationToken))
            .OnFailureAsync(err => err.AddError($"Не удалось выполнить поиск по подстроке '{substring}'."));

        return searchResult;
    }





    public async Task<Result<ClassificationTreeNode>> GetClassificationRootNode(CancellationToken cancellationToken)
    {
        var rootRequest = _requestBuilder.ClassificationRootNodeRequest();
        var rootResult = await _apiRepository.GetClassificationTreeNode(rootRequest, cancellationToken);
        if (!rootResult.IsSuccess)
        {
            return $"Не удалось получить корневой узел классификации из API Полинома: {rootResult.ErrorMessage}";
        }

        var rootItem = rootResult.Data!.Items.FirstOrDefault();
        return rootItem == null
            ? $"API Полинома прислал пустой список classification nodes при попытке поиска корневого справочника"
            : rootItem;
    }

    public Task<Result<List<ClassificationTreeNode>>> GetClassificationChildrenNodes(ClassificationTreeNode rootNode, CancellationToken cancellationToken)
    {
        var childrenRequest = _requestBuilder.ClassificationChildrenNodesRequest(rootNode.NodeObject, TreeFilterOptions.Name, string.Empty);
        return _apiRepository.GetClassificationNodeChildren(childrenRequest, cancellationToken)
            .OnFailureAsync(err => err.AddError("Не удалось получить дочерние узлы классификации из API Полинома."))
            .MapAsync(responseModel => responseModel.Items);
    }

    public Task<Result<List<ElementGroup>>> GetSubGroups(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
    {
        var request = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };
        return _apiRepository.GetGroupsInsideElementGroup(request, cancellationToken)
            .OnFailureAsync(err => err.AddError($"Не удалось получить подгруппы группы objectId '{objectId}' typeId '{typeId}'."));
    }

    public Task<Result<List<ElementCatalog>>> GetCatalogsByReference(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
    {
        var request = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };
        return _apiRepository.GetElementCatalogsByReference(request, cancellationToken)
            .OnFailureAsync(err => err.AddError($"Не удалось получить каталоги справочника objectId '{objectId}' typeId '{typeId}'."));
    }

    public Task<Result<List<ElementGroup>>> GetGroupsByCatalog(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
    {
        var request = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };
        return _apiRepository.GetElementGroupsByCatalog(request, cancellationToken)
            .OnFailureAsync(err => err.AddError($"Не удалось получить группы каталога objectId '{objectId}' typeId '{typeId}'."));
    }


    public Task<Result<PropertyOwnerResponseCustom>> GetAllPropertiesOfObject(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
    {
        var getPropertiesRequest = _requestBuilder.GetPropertiesRequest(objectId, typeId);
        return _apiRepository.GetAllPropertiesOfPropertyOwner(getPropertiesRequest, cancellationToken)
            .OnFailureAsync(err => err.AddError($"Не удалось получить понятия со свойствами для объекта objectId '{objectId}' typeId '{typeId}'."));
    }

    public Task<Result<List<IClassificationObject>>> GetParentGroups(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
    {
        var request = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };
        return _apiRepository.GetParentGroups(request, cancellationToken)
            .OnFailureAsync((err) => err.AddError($"Не удалось получить родительские группы объекта objectId '{objectId}' typeId '{typeId}'."));
    }

    public Task<Result<List<PolynomObjectWithShortProperties>>> GetParentGroupsWithProperties(
        int objectId,
        IdentifiableObjectType typeId,
        CancellationToken cancellationToken)
    {
        var parentGroupsRequest = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };

        return _apiRepository.GetParentGroups(parentGroupsRequest, cancellationToken)
            .OnFailureAsync(err => err.AddError($"Не удалось получить родительские группы объекта objectId '{objectId}' typeId '{typeId}'."))
            .BindAsync<List<IClassificationObject>, List<PolynomObjectWithShortProperties>>(async parentGroups =>
            {
                var resultList = new List<PolynomObjectWithShortProperties>();

                foreach (var parent in parentGroups)
                {
                    var propRequest = _requestBuilder.GetPropertiesRequest(parent.ObjectId, parent.TypeId);

                    var propsResult = await _apiRepository.GetAllPropertiesOfPropertyOwner(propRequest, cancellationToken);
                    if (!propsResult.IsSuccess)
                    {
                        propsResult.AddError($"Не удалось получить свойства группы с objectId \'{parent.ObjectId}\' и typeId \'{parent.TypeId}\' и именем \'{parent.Name}\'");
                        return propsResult.ErrorMessage;
                    }

                    // Если успех, добавляем собранный объект в список
                    var mappedObject = ModelMapper.CreateObjectWithShortProperties(parent, propsResult.Data!);
                    resultList.Add(mappedObject);
                }

                return resultList;
            });
    }

    public Task<Result<bool>> IsGroupLeafAsync(int objectId, IdentifiableObjectType typeId, CancellationToken cancellationToken)
    {
        var request = new IdentifierRequest { ObjectId = objectId, TypeId = typeId };
        return _apiRepository.GetGroupsInsideElementGroup(request, cancellationToken)
            .OnFailureAsync(err => err.AddError($"Не удалось проверить наличие подгрупп у группы objectId '{objectId}' typeId '{typeId}'."))
            .MapAsync(subGroups => subGroups.Count == 0);
    }

    public async Task<Result<string?>> GetLastClassificationCodeInGroup(int groupNodeObjectId, IdentifiableObjectType groupNodeTypeId, string minValue, string maxValue, CancellationToken cancellationToken)
    {
        var aaaa = new AccessControlObject { ObjectId = groupNodeObjectId, TypeId = groupNodeTypeId };
        string? result = null;
        bool hasNextPage = false;
        int pageNumber = 1;

        do
        {
            var request = _requestBuilder.ClassificationChildrenNodesRequest(aaaa, TreeFilterOptions.None, null,
                ClassificationTreeOptions.ShowViewpoints |
                ClassificationTreeOptions.ShowViewpointCatalog |
                ClassificationTreeOptions.ShowDocuments |
                ClassificationTreeOptions.ShowDocumentCatalog |
                ClassificationTreeOptions.ShowElements, // 410, так же, как делает запрос веб-клиент
                pageNumber);

            var childrenNodesResult = await _apiRepository.GetClassificationNodeChildren(request, cancellationToken)
                .OnFailureAsync(err => err.AddError("Не удалось получить дочерние узлы группы из API Полинома."));
            if (!childrenNodesResult.IsSuccess) return Result<string?>.Failure(childrenNodesResult.ErrorMessage);


            hasNextPage = childrenNodesResult.Data!.HasNextPage;

            foreach (var child in childrenNodesResult.Data!.Items)
            {
                var props = await GetAllPropertiesOfObject(child.NodeObject.ObjectId, child.NodeObject.TypeId, cancellationToken);
                if (!props.IsSuccess) return Result<string?>.Failure(props.ErrorMessage);

                var mappedProps = ModelMapper.CreateObjectWithShortProperties(child, props.Data!);
                var classificationContract = mappedProps.Contracts.Find(c => c.Name.Equals(_apiOptions.CurrentValue.ConceptNameForClassificationData, StringComparison.OrdinalIgnoreCase));
                var classificationCode = classificationContract?.Properties.Find(p => p.Name.Equals(_apiOptions.CurrentValue.ClassificationCodePropertyName, StringComparison.OrdinalIgnoreCase));
                if (classificationCode == null) continue;

                // Ошибочные данные — код вне допустимого диапазона группы, не учитываем при поиске максимума.
                if (NumericStringComparer.Instance.Compare(classificationCode.Value, minValue) < 0 ||
                    NumericStringComparer.Instance.Compare(classificationCode.Value, maxValue) > 0)
                    continue;

                int compRes = NumericStringComparer.Instance.Compare(result, classificationCode.Value);
                if (compRes < 0) result = classificationCode.Value;
                else continue;
            }

            pageNumber++;

        } while (hasNextPage);

        return Result<string?>.Success(result);
    }

    public async Task<Result<HashSet<string>>> GetAllClassificationCodesInGroup(int groupNodeObjectId, IdentifiableObjectType groupNodeTypeId, string minValue, string maxValue, CancellationToken cancellationToken)
    {
        var groupAccessObject = new AccessControlObject { ObjectId = groupNodeObjectId, TypeId = groupNodeTypeId };
        var result = new HashSet<string>();
        bool hasNextPage = false;
        int pageNumber = 1;

        do
        {
            var request = _requestBuilder.ClassificationChildrenNodesRequest(groupAccessObject, TreeFilterOptions.None, null,
                ClassificationTreeOptions.ShowViewpoints |
                ClassificationTreeOptions.ShowViewpointCatalog |
                ClassificationTreeOptions.ShowDocuments |
                ClassificationTreeOptions.ShowDocumentCatalog |
                ClassificationTreeOptions.ShowElements,
                pageNumber);

            var childrenNodesResult = await _apiRepository.GetClassificationNodeChildren(request, cancellationToken)
                .OnFailureAsync(err => err.AddError("Не удалось получить дочерние узлы группы из API Полинома."));
            if (!childrenNodesResult.IsSuccess) return Result<HashSet<string>>.Failure(childrenNodesResult.ErrorMessage);

            hasNextPage = childrenNodesResult.Data!.HasNextPage;

            foreach (var child in childrenNodesResult.Data!.Items)
            {
                var props = await GetAllPropertiesOfObject(child.NodeObject.ObjectId, child.NodeObject.TypeId, cancellationToken);
                if (!props.IsSuccess) return Result<HashSet<string>>.Failure(props.ErrorMessage);

                var mappedProps = ModelMapper.CreateObjectWithShortProperties(child, props.Data!);
                var classificationContract = mappedProps.Contracts.Find(c => c.Name.Equals(_apiOptions.CurrentValue.ConceptNameForClassificationData, StringComparison.OrdinalIgnoreCase));
                var classificationCode = classificationContract?.Properties.Find(p => p.Name.Equals(_apiOptions.CurrentValue.ClassificationCodePropertyName, StringComparison.OrdinalIgnoreCase));
                if (classificationCode?.Value == null) continue;

                // Ошибочные данные — код вне допустимого диапазона группы, не учитываем при поиске свободных номеров.
                if (NumericStringComparer.Instance.Compare(classificationCode.Value, minValue) < 0 ||
                    NumericStringComparer.Instance.Compare(classificationCode.Value, maxValue) > 0)
                    continue;

                result.Add(classificationCode.Value);
            }

            pageNumber++;

        } while (hasNextPage);

        return Result<HashSet<string>>.Success(result);
    }

    public async Task<Result<SetPropertyValuesResponse>> UpdateClassificationCodeAsync(
        PolynomObjectWithShortProperties updatedModel,
        PolynomContractWithShortProperties editedContract,
        PolynomShortProperty editedPropertyInContract,
        CancellationToken cancellationToken)
    {
        if (!editedContract.Properties.Contains(editedPropertyInContract))
        {
            throw new InvalidOperationException($"Переданное свойство {nameof(editedPropertyInContract)} не является частью переданного понятия {nameof(editedContract)}.");
        }

        // Получаем id свойства-источника для данного понятия
        var propertiesResult = await _apiRepository.GetConceptPropertiesByConceptId(editedContract.ObjectId, (int)editedContract.TypeId, cancellationToken);
        if (!propertiesResult.IsSuccess)
        {
            _logger.LogError("Не удалось получить свойства концепции {Concept}: {Error}", editedContract.Name, propertiesResult.ErrorMessage);
            return propertiesResult.ErrorMessage!;
        }

        _logger.LogInformation("Получены свойства концепции {Concept}. Количество: {Count}", editedContract.Name, propertiesResult.Data?.Count ?? 0);
        if (propertiesResult.Data != null)
        {
            foreach (var prop in propertiesResult.Data)
            {
                _logger.LogDebug("  Свойство: {Name} | ID: {ID} | IsReadOnly: {IsReadOnly} | IsReadOnlyEnabled: {IsReadOnlyEnabled}",
                    prop.Name, prop.ObjectId, prop.IsReadOnly, prop.IsReadOnlyEnabled);
            }
        }

        var propertySource = propertiesResult.Data?.FirstOrDefault(p =>
            p.Name.Equals(editedPropertyInContract.Name, StringComparison.OrdinalIgnoreCase));

        if (propertySource == null)
        {
            _logger.LogError("Не удалось найти свойство '{PropName}' в концепции '{Concept}'", editedPropertyInContract.Name, editedContract.Name);
            return $"Не удалось найти свойство '{editedPropertyInContract.Name}' в концепции '{editedContract.Name}'";
        }

        var propertySourceId = propertySource.ObjectId;
        var propertySourceTypeId = propertySource.TypeId;
        var wasUnlocked = false;

        _logger.LogInformation("Найденное свойство: {Name} | ID: {ID} | TypeId: {TypeId} | IsReadOnly: {IsReadOnly} | IsReadOnlyEnabled: {IsReadOnlyEnabled}",
            propertySource.Name, propertySourceId, propertySourceTypeId, propertySource.IsReadOnly, propertySource.IsReadOnlyEnabled);

        // Попытаемся разблокировать свойство, если оно заблокировано и флаг редактируемости включен
        if (propertySource.IsReadOnly && propertySource.IsReadOnlyEnabled)
        {
            _logger.LogInformation("Разблокировка свойства {PropName} (ID: {ID}, TypeId: {TypeId})", propertySource.Name, propertySourceId, propertySourceTypeId);
            var unlockResult = await _apiRepository.UpdateConceptPropertySource(propertySourceId, (int)propertySourceTypeId, false, cancellationToken);
            if (unlockResult.IsSuccess)
            {
                wasUnlocked = true;
                _logger.LogInformation("Свойство {PropName} успешно разблокировано", propertySource.Name);
            }
            else
            {
                _logger.LogError("Ошибка при разблокировке свойства {PropName}: {Error}", propertySource.Name, unlockResult.ErrorMessage);
            }
        }
        else
        {
            _logger.LogInformation("Свойство не требует разблокировки. IsReadOnly: {IsReadOnly}, IsReadOnlyEnabled: {IsReadOnlyEnabled}",
                propertySource.IsReadOnly, propertySource.IsReadOnlyEnabled);
        }

        try
        {
            var valueId = new IdentifiableObject { ObjectId = 1, TypeId = 0 };

            var request = new SetPropertyValuesRequest
            {
                AddedOwnConcepts = [],
                DeletedDynamicProperties = [],
                DeletedOwnConcepts = [],
                DeletedOwnProperties = [],
                Owner = new IdentifiableObject { ObjectId = updatedModel.ObjectId, TypeId = updatedModel.TypeId },
                Properties =
                [
                    new PropertyValueItem
                    {
                        Contract = new IdentifiableObject { ObjectId = editedContract.ObjectId, TypeId = editedContract.TypeId },
                        Definition = new IdentifiableObject { ObjectId = editedPropertyInContract.Definition.ObjectId, TypeId = editedPropertyInContract.Definition.TypeId },
                        Value = valueId,
                        EvaluationMode = (EvaluationMode)0
                    }
                ],
                Values = new AblePropertyValuesRequest { StringProperties = new Optional<List<IStringPropertyValueRequest>>(
                [
                    new StringPropertyValueRequest { Value = editedPropertyInContract.Value, ObjectId = valueId.ObjectId, TypeId = valueId.TypeId }
                ])}
            };

            var updateResult = await _apiRepository.SetPropertyValuesOfPropertyOwner(request, cancellationToken);
            _logger.LogInformation("Результат обновления свойства: {IsSuccess}", updateResult.IsSuccess);
            return updateResult;
        }
        finally
        {
            // Вернём блокировку, если мы её снимали
            if (wasUnlocked)
            {
                _logger.LogInformation("Возврат блокировки для свойства (ID: {ID}, TypeId: {TypeId})", propertySourceId, propertySourceTypeId);
                var lockResult = await _apiRepository.UpdateConceptPropertySource(propertySourceId, (int)propertySourceTypeId, true, cancellationToken);
                if (lockResult.IsSuccess)
                {
                    _logger.LogInformation("Свойство успешно заблокировано");
                }
                else
                {
                    _logger.LogError("Ошибка при блокировке свойства: {Error}", lockResult.ErrorMessage);
                }
            }
        }
    }
}
