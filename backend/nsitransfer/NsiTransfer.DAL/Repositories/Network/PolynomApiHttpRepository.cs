using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.PropertyOwner;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Tree;
using Ascon.Polynom.Web.Api.Data.Json;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Classification;
using Ascon.Polynom.Web.Api.Data.Models.Properties;
using Ascon.Polynom.Web.Api.Data.Models.Properties.Definition;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using Ascon.Polynom.Web.Api.Data.Models.TreeView;
using Ascon.Polynom.Web.Api.Data.Requests.Base;
using Ascon.Polynom.Web.Api.Data.Requests.Search;
using Ascon.Polynom.Web.Api.Data.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NsiTransfer.Contract.Models.Ascon;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Interfaces.Http;
using NsiTransfer.DAL.Routes;
using System.Text.Json;

namespace NsiTransfer.DAL.Repositories.Network;

internal class PolynomApiHttpRepository : BaseHttpRepository, IPolynomApiHttpRepository
{
    private readonly IPolynomHttpClient _apiHttpClient;

    public PolynomApiHttpRepository(IPolynomHttpClientFactory httpClientFactory, ILogger<BaseHttpRepository> logger) : base(logger)
    {
        _apiHttpClient = httpClientFactory.Create(PolynomHttpClientKeys.Api);
    }

    public Task<Result<PaginatedList<PropertySearchResultObject>>> ExecuteSearchProperty(
        PropertySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | SearchProperty", ApiRoutes.SearchPropertyQuery);
        try
        {
            var requestJson = JsonSerializer.Serialize(request, PolynomJsonOptions.Options);
            Logger.LogInformation("[API] REQUEST BODY: {RequestBody}", requestJson);
        }
        catch { }
        return SendAndDeserializeAsync<PaginatedList<PropertySearchResultObject>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.SearchPropertyQuery, request, cancellationToken),
            ApiRoutes.SearchPropertyQuery,
            "SearchProperty",
            cancellationToken);
    }

    public Task<Result<PaginatedList<ClassificationTreeNode>>> GetClassificationTreeNode(
        IClassificationTreeRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetClassificationTreeNode (корневой узел)", ApiRoutes.GetClassification);
        return SendAndDeserializeAsync<PaginatedList<ClassificationTreeNode>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetClassification, request, cancellationToken),
            ApiRoutes.GetClassification,
            "GetClassificationTreeNode",
            cancellationToken);
    }

    public Task<Result<PaginatedList<ClassificationTreeNode>>> GetClassificationNodeChildren(
        IClassificationNodeChildrenRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetClassificationNodeChildren (дочерние узлы)", ApiRoutes.GetClassificationNodeChildren);
        return SendAndDeserializeAsync<PaginatedList<ClassificationTreeNode>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetClassificationNodeChildren, request, cancellationToken),
            ApiRoutes.GetClassificationNodeChildren,
            "GetClassificationNodeChildren",
            cancellationToken);
    }

    public Task<Result<PropertyDefinition>> GetPropertyDefinition(
        IGetByAbsoluteCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetPropertyDefinition (определение свойства по коду)", ApiRoutes.GetPropertyDefinitionByAbsoluteCode);
        return SendAndDeserializeAsync<PropertyDefinition>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetPropertyDefinitionByAbsoluteCode, request, cancellationToken),
            ApiRoutes.GetPropertyDefinitionByAbsoluteCode,
            "GetPropertyDefinition",
            cancellationToken);
    }

    public Task<Result<ConceptPropertySource>> GetConceptPropertySource(GetByAbsoluteCodeRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetConceptPropertySource (источник свойств концепции)", ApiRoutes.ConceptPropertySourceByAbsoluteCode);
        return SendAndDeserializeAsync<ConceptPropertySource>(
            () => _apiHttpClient.PostAsync(ApiRoutes.ConceptPropertySourceByAbsoluteCode, request, cancellationToken),
            ApiRoutes.ConceptPropertySourceByAbsoluteCode,
            "GetConceptPropertySource",
            cancellationToken);
    }

    public Task<Result<List<ConceptPropertySource>>> GetConceptPropertiesByConceptId(int conceptObjectId, int conceptTypeId, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetConceptPropertiesByConceptId (свойства концепции)", ApiRoutes.ConceptPropertySourceByConceptId);
        var request = new { objectId = conceptObjectId, typeId = conceptTypeId };
        return SendAndDeserializeAsync<List<ConceptPropertySource>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.ConceptPropertySourceByConceptId, request, cancellationToken),
            ApiRoutes.ConceptPropertySourceByConceptId,
            "GetConceptPropertiesByConceptId",
            cancellationToken);
    }

    public Task<Result<PropertyOwnerResponseCustom>> GetAllPropertiesOfPropertyOwner(IGetPropertiesRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetAllPropertiesOfPropertyOwner (свойства объекта)", ApiRoutes.GetPropertiesOfPropertyOwner);
        return SendAndDeserializeAsync<PropertyOwnerResponseCustom>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetPropertiesOfPropertyOwner, request, cancellationToken),
            ApiRoutes.GetPropertiesOfPropertyOwner,
            "GetAllPropertiesOfPropertyOwner",
            cancellationToken);
    }

    public Task<Result<List<IClassificationObject>>> GetParentGroups(IIdentifierRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetParentGroups (родительские группы)", ApiRoutes.GetParentGroups);
        return SendAndDeserializeAsync<List<IClassificationObject>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetParentGroups, request, cancellationToken),
            ApiRoutes.GetParentGroups,
            "GetParentGroups",
            cancellationToken);
    }

    public Task<Result<List<ElementGroup>>> GetGroupsInsideElementGroup(IIdentifierRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetGroupsInsideElementGroup (подгруппы группы элементов)", ApiRoutes.GetGroupsInsideElementGroup);
        return SendAndDeserializeAsync<List<ElementGroup>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetGroupsInsideElementGroup, request, cancellationToken),
            ApiRoutes.GetGroupsInsideElementGroup,
            "GetGroupsInsideElementGroup",
            cancellationToken);
    }

    public Task<Result<List<ElementCatalog>>> GetElementCatalogsByReference(IIdentifierRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetElementCatalogsByReference (каталоги справочника)", ApiRoutes.GetElementCatalogsByReference);
        return SendAndDeserializeAsync<List<ElementCatalog>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetElementCatalogsByReference, request, cancellationToken),
            ApiRoutes.GetElementCatalogsByReference,
            "GetElementCatalogsByReference",
            cancellationToken);
    }

    public Task<Result<List<ElementGroup>>> GetElementGroupsByCatalog(IIdentifierRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | GetElementGroupsByCatalog (группы верхнего уровня каталога)", ApiRoutes.GetElementGroupsByCatalog);
        return SendAndDeserializeAsync<List<ElementGroup>>(
            () => _apiHttpClient.PostAsync(ApiRoutes.GetElementGroupsByCatalog, request, cancellationToken),
            ApiRoutes.GetElementGroupsByCatalog,
            "GetElementGroupsByCatalog",
            cancellationToken);
    }

    public Task<Result<SetPropertyValuesResponse>> SetPropertyValuesOfPropertyOwner(ISetPropertyValuesRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | SetPropertyValuesOfPropertyOwner (установить код классификатора)", ApiRoutes.SetPropertyValues);
        return SendAndDeserializeAsync<SetPropertyValuesResponse>(
            () => _apiHttpClient.PostAsync(ApiRoutes.SetPropertyValues, request, cancellationToken),
            ApiRoutes.SetPropertyValues,
            "SetPropertyValuesOfPropertyOwner",
            cancellationToken);
    }

    public async Task<Result<bool>> UpdateConceptPropertySource(int propertySourceId, int typeId, bool isReadOnly, CancellationToken cancellationToken)
    {
        Logger.LogInformation("[API] → POST {Endpoint} | UpdateConceptPropertySource (переключить флаг \"Запретить изменение\")", ApiRoutes.ConceptPropertySourceUpdate);

        var request = new
        {
            conceptPropertySource = new { objectId = propertySourceId, typeId = typeId },
            isReadOnly = isReadOnly
        };

        var httpResult = await SendRequestAsync(
            () => _apiHttpClient.PostAsync(ApiRoutes.ConceptPropertySourceUpdate, request, cancellationToken),
            cancellationToken);

        if (!httpResult.IsSuccess)
        {
            Logger.LogError("[API] ✗ UpdateConceptPropertySource | {Endpoint} | Ошибка отправки: {Error}", ApiRoutes.ConceptPropertySourceUpdate, httpResult.ErrorMessage);
            return httpResult.ErrorMessage!;
        }

        var response = httpResult.Data!;
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError("[API] ✗ UpdateConceptPropertySource | {Endpoint} | Статус: {Status} | Ответ: {Content}", ApiRoutes.ConceptPropertySourceUpdate, response.StatusCode, await response.Content.ReadAsStringAsync());
            return $"Ошибка API на endpoint {ApiRoutes.ConceptPropertySourceUpdate}. Статус: {response.StatusCode}.";
        }

        Logger.LogInformation("[API] ✓ UpdateConceptPropertySource | {Endpoint} | Статус: 200 OK", ApiRoutes.ConceptPropertySourceUpdate);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Универсальный метод для отправки POST-запроса и десериализации ответа.
    /// </summary>
    private async Task<Result<T>> SendAndDeserializeAsync<T>(
        Func<Task<HttpResponseMessage>> requestFactory,
        string endpoint,
        string methodName,
        CancellationToken cancellationToken)
    {
        var httpResult = await SendRequestAsync(requestFactory, cancellationToken);
        if (!httpResult.IsSuccess)
        {
            var errorMsg = $"Не удалось отправить запрос на endpoint {endpoint}. Ошибка: {httpResult.ErrorMessage}";
            Logger.LogError("[API] ✗ {Method} | {Endpoint} | Ошибка отправки: {Error}", methodName, endpoint, httpResult.ErrorMessage);
            return errorMsg;
        }

        var response = httpResult.Data!;

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError("[API] ✗ {Method} | {Endpoint} | Статус: {StatusCode} | Ответ: {Error}", methodName, endpoint, (int)response.StatusCode, errorContent);
            try
            {
                var errorResult = JsonSerializer.Deserialize<ProblemDetails>(errorContent, PolynomJsonOptions.Options);
                return $"Ошибка API на endpoint {endpoint}. Статус: {(int)response.StatusCode}. Ответ: {(errorResult == null ? errorContent : errorResult.Detail)}";
            }
            catch (Exception ex)
            {
                return $"Ошибка API на endpoint {endpoint}. Статус: {(int)response.StatusCode}. Ответ: {errorContent};\nDeserializing exception of ProblemDetails response model: {ex.Message}";
            }
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            // Логируем часть ответа (первые 3000 символов для больших ответов)
            var responsePreview = content.Length > 3000 ? content.Substring(0, 3000) + "..." : content;
            Logger.LogInformation("[API] RESPONSE BODY: {ResponseBody}", responsePreview);

            var result = JsonSerializer.Deserialize<T>(content, PolynomJsonOptions.Options);
            if (result == null)
            {
                var errorMsg = $"Получен пустой ответ при десериализации с endpoint {endpoint}.";
                Logger.LogWarning("[API] ⚠ {Method} | {Endpoint} | Пустой ответ", methodName, endpoint);
                return errorMsg;
            }

            Logger.LogInformation("[API] ✓ {Method} | {Endpoint} | Статус: 200 OK | Размер: {Size} байт", methodName, endpoint, content.Length);
            return result;
        }
        catch (JsonException ex)
        {
            Logger.LogError("[API] ✗ {Method} | {Endpoint} | Ошибка десериализации JSON: {Error}", methodName, endpoint, ex.Message);
            return $"Не удалось десериализовать ответ с endpoint {endpoint}. Ошибка JSON: {ex.Message}";
        }
    }
}