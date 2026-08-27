using Ascon.Polynom.Web.Api.Data;
using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Properties.Values;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.PropertyOwner;
using Ascon.Polynom.Web.Api.Data.Interfaces.Requests.Tree;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Properties;
using Ascon.Polynom.Web.Api.Data.Models.Properties.Definition;
using Ascon.Polynom.Web.Api.Data.Models.Properties.Values;
using Ascon.Polynom.Web.Api.Data.Requests.Base;
using Ascon.Polynom.Web.Api.Data.Requests.Properties.Values;
using Ascon.Polynom.Web.Api.Data.Requests.PropertyOwner;
using Ascon.Polynom.Web.Api.Data.Requests.Search;
using Ascon.Polynom.Web.Api.Data.Requests.Tree;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Interfaces.Http;

namespace NsiTransfer.BLL.Tools;

public class PolynomRequestBuilder : IPolynomRequestBuilder
{
    private readonly IOptionsHelper<AppConfiguration> _optionsHelper;
    private readonly IPolynomApiHttpRepository _apiRepository;
    private readonly PolynomTimeConverter _timeConverter;

    public PolynomRequestBuilder(
        IOptionsHelper<AppConfiguration> optionsHelper,
        IPolynomApiHttpRepository apiRepository,
        PolynomTimeConverter timeCoverter)
    {
        _optionsHelper = optionsHelper;
        _apiRepository = apiRepository;
        _timeConverter = timeCoverter;
    }


    public IIdentifiableObject BuildIdentifiableObjectModel(int objectId, int typeId) => new IdentifiableObject { ObjectId = objectId, TypeId = (IdentifiableObjectType)typeId };

    public IIdentifiableObject BuildIdentifiableObjectModel(int objectId, IdentifiableObjectType typeId) => new IdentifiableObject { ObjectId = objectId, TypeId = typeId };


    public Task<Result<PropertySearchRequest>> TimePeriodRequest_Test(DateTime from, DateTime to, int pageSize = 100)
    {
        var absoluteCode = "pd:@DateModified";
        return GetOwnerScopeFromConfiguration()
            .AndWithAsync(_ => GetPropertyDefinition(absoluteCode), (t1, t2) => (ownerScope: t1, propertyDefinition: t2))
            .MapAsync(tuple => new PropertySearchRequest
            {
                OwnerScope = tuple.ownerScope,

                Condition = new ComplexConditionRequest
                {
                    ComplexConditions =
                [
                    new ComplexConditionRequest
                    {
                        SimpleConditions =
                        [
                            new SimpleConditionRequest { Definition = tuple.propertyDefinition, Enabled = true, Operation = 6, Options = 0, Value = BuildIdentifiableObjectModel(0, 0) },
                            new SimpleConditionRequest { Definition = tuple.propertyDefinition, Enabled = true, Operation = 4, Options = 0, Value = BuildIdentifiableObjectModel(1, 0) },
                        ],
                        Enabled = true,
                    }
                ],
                    Enabled = true
                },

                Values = new AblePropertyValuesRequest
                {
                    DateTimeProperties = new Optional<List<IDateTimePropertyValueRequest>>([
                    new DateTimePropertyValueRequest { ObjectId = 0, Value = new DateTimeValue { UseTime = true, Value = from } },
                    new DateTimePropertyValueRequest { ObjectId = 1, Value = new DateTimeValue { UseTime = true, Value = to } }
                ])
                },

                PageNumber = 1,
                PageSize = pageSize
            });
    }


    public Task<Result<PropertySearchRequest>> TimePeriodRequest(DateTime from, DateTime to, int pageSize = 100, int pageNumber = 1)
    {
        var absoluteCode = "c:@NameAndDescription::c:@ClassificationItem::pd:@DateModified";
        return GetOwnerScopeFromConfiguration()
            .AndWithAsync(_ => GetConceptPropertySource(absoluteCode), (t1, t2) => (ownerScope: t1, conceptProperty: t2))
            .MapAsync(tuple => new PropertySearchRequest
            {
                OwnerScope = tuple.ownerScope,

                Condition = new ComplexConditionRequest
                {
                    IntersectionType = IntersectionType.And,
                    ComplexConditions =
                    [
                        new ComplexConditionRequest
                        {
                            SimpleConditions =
                            [
                                new SimpleConditionRequest { Enabled = true, Operation = 6, Options = 0, SearchConditionTargetQualifier = tuple.conceptProperty, Value = BuildIdentifiableObjectModel(0, 0) },
                                new SimpleConditionRequest { Enabled = true, Operation = 4, Options = 0, SearchConditionTargetQualifier = tuple.conceptProperty, Value = BuildIdentifiableObjectModel(1, 0) }
                            ],
                            Enabled = false
                        }
                    ],
                    Enabled = true
                },

                Values = new AblePropertyValuesRequest
                {
                    DateTimeProperties = new Optional<List<IDateTimePropertyValueRequest>>([
                    new DateTimePropertyValueRequest { ObjectId = 0, Value = new DateTimeValue { UseTime = true, Value = _timeConverter.ConvertToPolynomTime(from) } },
                    new DateTimePropertyValueRequest { ObjectId = 1, Value = new DateTimeValue { UseTime = true, Value = _timeConverter.ConvertToPolynomTime(to) } }
                ])
                },

                PageNumber = pageNumber,
                PageSize = pageSize
            });
    }

    public Task<Result<PropertySearchRequest>> ConcreteTimeSearchRequest(DateTime value, int pageSize = 100, int pageNumber = 1)
    {
        var absoluteCode = "c:@NameAndDescription::c:@ClassificationItem::pd:@DateModified";
        return GetOwnerScopeFromConfiguration()
            .AndWithAsync(_ => GetConceptPropertySource(absoluteCode), (t1, t2) => (ownerScope: t1, conceptProperty: t2))
            .MapAsync(tuple => new PropertySearchRequest
            {
                // objectId = 1 and typeId = 203 это "Все справочники"
                OwnerScope = tuple.ownerScope,

                Condition = new ComplexConditionRequest
                {
                    ComplexConditions = [],
                    ElementConditions = [],
                    PropValueConditions = [],
                    SimpleConditions =
                    [
                        new SimpleConditionRequest
                        {
                            Enabled = true,
                            SearchConditionTargetQualifier = tuple.conceptProperty,
                            Definition = null,
                            Operation = 1,
                            Options = 0,
                            Value = BuildIdentifiableObjectModel(0, 0)
                        }
                    ],
                    IntersectionType = 0,
                    Enabled = true
                },

                Values = new AblePropertyValuesRequest
                {
                    DateTimeProperties = new Optional<List<IDateTimePropertyValueRequest>>([
                        new DateTimePropertyValueRequest { ObjectId = 0, Value = new DateTimeValue { Value = _timeConverter.ConvertToPolynomTime(value), UseTime = true } }
                    ])
                },

                PageNumber = pageNumber,
                PageSize = pageSize
            });
    }

    public Task<Result<PropertySearchRequest>> NameContainsSubstringSearchRequest(string substring, int pageSize = 10, int pageNumber = 1)
    {
        var absoluteCode = "c:@NameAndDescription::c:@ClassificationItem::pd:@Name";
        return GetOwnerScopeFromConfiguration()
            .AndWithAsync(_ => GetConceptPropertySource(absoluteCode), (t1, t2) => (ownerScope: t1, conceptProperty: t2))
            .MapAsync(tuple => new PropertySearchRequest
            {
                // objectId = 1 and typeId = 203 это "Все справочники"
                OwnerScope = tuple.ownerScope,

                Condition = new ComplexConditionRequest
                {
                    ComplexConditions = [],
                    ElementConditions = [],
                    PropValueConditions = [],
                    SimpleConditions =
                [
                    new SimpleConditionRequest
                    {
                        Enabled = true,
                        SearchConditionTargetQualifier = tuple.conceptProperty,
                        Definition = null,
                        Operation = 3,
                        Options = 0,
                        Value = BuildIdentifiableObjectModel(0, 0)
                    }
                ],
                    IntersectionType = 0,
                    Enabled = true
                },

                Values = new AblePropertyValuesRequest
                {
                    StringProperties = new Optional<List<IStringPropertyValueRequest>>([
                    new StringPropertyValueRequest
                    {
                        ObjectId = 0,
                        TypeId = IdentifiableObjectType.StringPropertyDefinition,
                        Value = substring
                    }
                ])
                },

                PageNumber = pageNumber,
                PageSize = pageSize
            });
    }


    public IClassificationTreeRequest ClassificationRootNodeRequest()
    {
        var req = new ClassificationTreeRequest
        {
            FilterOptions = TreeFilterOptions.Name,
            FilterString = string.Empty,
            Options = ClassificationTreeOptions.Default, // 8A в 16-чной = 136 в 10-чной, такое значение уходит от веб-клиента Полином
            PageNumber = 0,
            PageSize = 0
        };

        return req;
    }

    public IClassificationNodeChildrenRequest ClassificationChildrenNodesRequest(
        IAccessControlObject nodeObject,
        TreeFilterOptions filterOptions,
        string? filterString,
        ClassificationTreeOptions options = ClassificationTreeOptions.Default,
        int pageNumber = 1)
    {
        var req = new ClassificationNodeChildrenRequest
        {
            FilterOptions = filterOptions,
            FilterString = filterString,
            Options = options,
            PageNumber = pageNumber,
            PageSize = 100,
            StartIndex = 0,
            ParentNodeObject = nodeObject
        };

        return req;
    }


    public IGetPropertiesRequest GetPropertiesRequest(int objectId, IdentifiableObjectType typeId)
    {
        return new GetPropertiesRequest { Owner = BuildIdentifiableObjectModel(objectId, typeId) };
    }




    private Result<IIdentifiableObject> GetOwnerScopeFromConfiguration()
    {
        if (!_optionsHelper.TryGetCurrentValue(out var config))
        {
            return "Не удалось получить конфигурацию для определения справочника, в котором должен осуществляться поиск объектов Полином: файл конфигурации содержит невалидные данные";
        }

        var node = config!.TargetReferenceNode;
        if (node.TargetReferenceNodeObjectId == 0 || node.TargetReferenceNodeTypeId == 0)
        {
            return "Справочник, в котором должен осуществляться поиск объектов Полином, не настроен в конфигурации сервиса. Перед созданием задачи на синхронизацию необходимо указать этот справочник";
        }

        IIdentifiableObject result = new IdentifiableObject
        {
            ObjectId = node.TargetReferenceNodeObjectId,
            TypeId = (IdentifiableObjectType)node.TargetReferenceNodeTypeId
        };

        return Result<IIdentifiableObject>.Success(result);
    }

    private async Task<Result<PropertyDefinition>> GetPropertyDefinition(string absoluteCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(absoluteCode))
        {
            return "Параметр absoluteCode не может быть пустым";
        }

        try
        {
            var request = new GetByAbsoluteCodeRequest { AbsoluteCode = absoluteCode };
            var result = await _apiRepository.GetPropertyDefinition(request, cancellationToken);

            return result.IsSuccess
                ? result
                : $"Не удалось получить PropertyDefinition по коду '{absoluteCode}': {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            return $"Исключение при получении PropertyDefinition: {ex.Message}";
        }
    }

    private async Task<Result<ConceptPropertySource>> GetConceptPropertySource(string absoluteCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(absoluteCode))
        {
            return "Параметр absoluteCode не может быть пустым";
        }

        try
        {
            var request = new GetByAbsoluteCodeRequest { AbsoluteCode = absoluteCode };
            var result = await _apiRepository.GetConceptPropertySource(request, cancellationToken);

            return result.IsSuccess
                ? result
                : $"Не удалось получить ConceptPropertySource по коду '{absoluteCode}': {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            return $"Исключение при получении ConceptPropertySource: {ex.Message}";
        }
    }
}
