using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using NsiTransfer.Contract.Models.Ascon;
using NsiTransfer.Contract.Models.DTO;

namespace NsiTransfer.BLL.Tools;

internal class ModelMapper
{
    public static PolynomObjectWithShortProperties CreateObjectWithShortProperties(INamedObject obj, PropertyOwnerResponseCustom props)
    {
        var resultObj = new PolynomObjectWithShortProperties
        {
            ObjectId = obj.ObjectId,
            TypeId = obj.TypeId,
            Name = obj.Name
        };

        resultObj.Contracts = props.AllContracts.Select(contract => new PolynomContractWithShortProperties
        {
            ObjectId = contract.ObjectId,
            TypeId = contract.TypeId,
            Name = contract.Name,

            Properties = CreateShortProperties(
                contract,
                props.PropertyOwner.Properties
                    .Where(p => p.Contract != null
                            && p.Contract.ObjectId == contract.ObjectId
                            && p.Contract.TypeId == contract.TypeId),
                obj,
                props
                )
        }).ToList();

        return resultObj;
    }

    private static List<PolynomShortProperty> CreateShortProperties(
        IContractRefCustom contract, 
        IEnumerable<IPropertyRef> properties,
        INamedObject obj,
        PropertyOwnerResponseCustom props)
    {
        var result = new List<PolynomShortProperty>();

        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var defaultMeasureUnit = contract.Properties
                .Find(p => prop.ContractPropertySource != null
                        && p.ObjectId == prop.ContractPropertySource.ObjectId
                        && p.TypeId == prop.ContractPropertySource.TypeId)
                ?.DefaultMeasureUnit;
            var propertyDefinition = prop.Definition;

            var measureUnit = GetMeasureUnit(defaultMeasureUnit, obj, props);
            var (value, description, propertyType, classifId) = GetValueAndDescription(propertyDefinition, obj, props);

            if (string.Equals(propName, "Применяемость", StringComparison.OrdinalIgnoreCase))
            {
                classifId = value switch
                {
                    "Разрешен к применению" => "0",
                    "Запрещен к применению" => "1",
                    "Ограничено разрешен" => "1",
                    _ => classifId
                };
            }

            var shortProperty = new PolynomShortProperty
            {
                Name = propName,
                Value = value,

                MeasureUnitDesignation = measureUnit.HasValue ? measureUnit.Value.Designation : null,
                MeasureUnitName = measureUnit.HasValue ? measureUnit.Value.Name : null,

                Definition = new Contract.Models.Common.PolynomObject(propertyDefinition?.ObjectId ?? 0, propertyDefinition?.TypeId ?? 0),

                PropertyTypeId = propertyType,
                PropertyTypeName = propertyType?.ToString(),

                Description = description,
                ClassifId = classifId
            };

            result.Add(shortProperty);
        }

        return result;
    }

    private static (string Designation, string Name)? GetMeasureUnit(
        IIdentifiableObject? defaultMeasureUnit,
        INamedObject obj, 
        PropertyOwnerResponseCustom props)
    {
        // У свойства может не быть единицы измерения, тогда defaultMeasureUnit будет null -> возврат пустой единицы измерения
        if (defaultMeasureUnit == null) return null;
        var measureUnit = props.MeasureUnits.Units.FirstOrDefault(u => u.ObjectId == defaultMeasureUnit.ObjectId
                                                                    && u.TypeId == defaultMeasureUnit.TypeId)
                            ?? throw new InvalidDataException($"Не удалось найти необходимую сущность из списка MeasureUnits.Units " +
                                                                $"с ObjectId \'{defaultMeasureUnit.ObjectId}\' и TypeId \'{defaultMeasureUnit.TypeId}\' " +
                                                                $"для объекта с ObjectId \'{obj.ObjectId}\' и TypeId \'{obj.TypeId}\' с именем \'{obj.Name}\'");
        return (measureUnit.Designation, measureUnit.Name);
    }

    private static (string? Value, string? Description, PropertyType? PropertyType, string? ClassifId) GetValueAndDescription(
        IIdentifiableObject? propertyDefinition,
        INamedObject obj,
        PropertyOwnerResponseCustom props)
    {
        var property = props.PropertyOwner.Properties.FirstOrDefault(p => (p.Definition?.ObjectId ?? 0) == (propertyDefinition?.ObjectId ?? 0)
                                                && (p.Definition?.TypeId ?? 0) == (propertyDefinition?.TypeId ?? 0));

        if (property == null) return (null, null, null, null);

        var valueIds = property.Value;
        var propertyType = property.Type;

        var propertyValue = SeparatePureValue(valueIds, propertyType, obj, props);
        var propertyDescription = SeparatePureDescription(propertyType, propertyDefinition, obj, props);
        var classifId = SeparateEnumStringClassifId(valueIds, propertyType, obj, props);

        return (propertyValue, propertyDescription, propertyType, classifId);
    }

    private static string? SeparatePureValue(
        IIdentifiableObject? valueIds,
        PropertyType propertyType,
        INamedObject obj,
        PropertyOwnerResponseCustom props)
    {
        if (valueIds == null) return null;

        switch (propertyType)
        {
            case PropertyType.Binary when props.Values.BinaryProperties.HasValue:
                return FindProperty(props.Values.BinaryProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.BinaryProperties", valueIds, obj).Value;

            case PropertyType.Boolean when props.Values.BooleanProperties.HasValue:
                return FindProperty(props.Values.BooleanProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.BooleanProperties", valueIds, obj).Value.ToString();

            case PropertyType.Color when props.Values.ColorProperties.HasValue:
                return FindProperty(props.Values.ColorProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.ColorProperties", valueIds, obj).Value?.ToString();

            case PropertyType.DateTime when props.Values.DateTimeProperties.HasValue:
                var dateTimeValue = props.Values.DateTimeProperties.Value?.FirstOrDefault(v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId);
                return dateTimeValue?.Value?.Value.ToString();

            case PropertyType.Double when props.Values.DoubleProperties.HasValue:
                var doubleValue = props.Values.DoubleProperties.Value?.FirstOrDefault(p => p.ObjectId == valueIds.ObjectId && p.TypeId == valueIds.TypeId);
                return doubleValue?.Value?.Value.ToString();

            case PropertyType.Enum when props.Values.EnumProperties.HasValue:
                return FindProperty(props.Values.EnumProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.EnumProperties", valueIds, obj).Value?.ToString();

            case PropertyType.EnumBool when props.Values.EnumBoolProperties.HasValue:
                return FindProperty(props.Values.EnumBoolProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.EnumBoolProperties", valueIds, obj).Value?.Value.ToString();

            case PropertyType.EnumDouble when props.Values.EnumDoubleProperties.HasValue:
                return FindProperty(props.Values.EnumDoubleProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.EnumDoubleProperties", valueIds, obj).Value?.Value.ToString();

            case PropertyType.EnumInt when props.Values.EnumIntProperties.HasValue:
                return FindProperty(props.Values.EnumIntProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.EnumIntProperties", valueIds, obj).Value?.Value.ToString();

            case PropertyType.EnumString when props.Values.EnumStringProperties.HasValue:
                return FindProperty(props.Values.EnumStringProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.EnumStringProperties", valueIds, obj).Value?.Value;

            case PropertyType.Guid when props.Values.GuidProperties.HasValue:
                return FindProperty(props.Values.GuidProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.GuidProperties", valueIds, obj).Value?.ToString();

            case PropertyType.Image when props.Values.ImageProperties.HasValue:
                return FindProperty(props.Values.ImageProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.ImageProperties", valueIds, obj).Value?.ToString();

            case PropertyType.Integer when props.Values.IntegerProperties.HasValue:
                return FindProperty(props.Values.IntegerProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.IntegerProperties", valueIds, obj).Value.ToString();

            case PropertyType.Null:
                return null;

            case PropertyType.Optic when props.Values.OpticProperties.HasValue:
                return FindProperty(props.Values.OpticProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.OpticProperties", valueIds, obj).Value?.ToString();

            case PropertyType.Rtf when props.Values.RtfProperties.HasValue:
                return FindProperty(props.Values.RtfProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.RtfProperties", valueIds, obj).Value?.ToString();

            case PropertyType.Set when props.Values.SetProperties.HasValue:
                return FindProperty(props.Values.SetProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.SetProperties", valueIds, obj).Value?.ToString();

            case PropertyType.String when props.Values.StringProperties.HasValue:
                return FindProperty(props.Values.StringProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.StringProperties", valueIds, obj).Value?.ToString();

            case PropertyType.Table when props.Values.TableProperties.HasValue:
                return FindProperty(props.Values.TableProperties.Value, v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId, "Values.TableProperties", valueIds, obj).Value?.ToString();

            default:
                return null;
        }
    }

    private static string? SeparatePureDescription(
        PropertyType propertyType,
        IIdentifiableObject propDef,
        INamedObject obj,
        PropertyOwnerResponseCustom props)
    {
        switch (propertyType)
        {
            case PropertyType.Binary:
                return FindProperty(props.Definitions.BinaryProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.BinaryProperties", propDef, obj).Description;

            case PropertyType.Boolean:
                return FindProperty(props.Definitions.BooleanProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.BooleanProperties", propDef, obj).Description;

            case PropertyType.Color:
                return FindProperty(props.Definitions.ColorProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.ColorProperties", propDef, obj).Description;

            case PropertyType.DateTime:
                return FindProperty(props.Definitions.DateTimeProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.DateTimeProperties", propDef, obj).Description;

            case PropertyType.Double:
                return FindProperty(props.Definitions.DoubleProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.DoubleProperties", propDef, obj).Description;

            case PropertyType.Enum:
                return FindProperty(props.Definitions.EnumProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.EnumProperties", propDef, obj).Description;

            case PropertyType.EnumBool:
                return FindProperty(props.Definitions.EnumBoolProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.EnumBoolProperties", propDef, obj).Description;

            case PropertyType.EnumDouble:
                return FindProperty(props.Definitions.EnumDoubleProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.EnumDoubleProperties", propDef, obj).Description;

            case PropertyType.EnumInt:
                return FindProperty(props.Definitions.EnumIntProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.EnumIntProperties", propDef, obj).Description;

            case PropertyType.EnumString:
                return FindProperty(props.Definitions.EnumStringProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.EnumStringProperties", propDef, obj).Description;

            case PropertyType.Guid:
                return FindProperty(props.Definitions.GuidProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.GuidProperties", propDef, obj).Description;

            case PropertyType.Image:
                return FindProperty(props.Definitions.ImageProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.ImageProperties", propDef, obj).Description;

            case PropertyType.Integer:
                return FindProperty(props.Definitions.IntegerProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.IntegerProperties", propDef, obj).Description;

            case PropertyType.Null:
                return null;

            case PropertyType.Optic:
                return FindProperty(props.Definitions.OpticProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.OpticProperties", propDef, obj).Description;

            case PropertyType.Rtf:
                return FindProperty(props.Definitions.RtfProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.RtfProperties", propDef, obj).Description;

            case PropertyType.Set:
                return FindProperty(props.Definitions.SetProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.SetProperties", propDef, obj).Description;

            case PropertyType.String:
                return FindProperty(props.Definitions.StringProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.StringProperties", propDef, obj).Description;

            case PropertyType.Table:
                return FindProperty(props.Definitions.TableProperties, p => p.ObjectId == propDef.ObjectId && p.TypeId == propDef.TypeId, "Definitions.TableProperties", propDef, obj).Description;

            default:
                return null;
        }
    }

    private static string? SeparateEnumStringClassifId(
        IIdentifiableObject? valueIds,
        PropertyType propertyType,
        INamedObject obj,
        PropertyOwnerResponseCustom props)
    {
        if (valueIds == null || propertyType != PropertyType.EnumString || !props.Values.EnumStringProperties.HasValue)
            return null;

        var enumStringProperty = FindProperty(
            props.Values.EnumStringProperties.Value,
            v => v.ObjectId == valueIds.ObjectId && v.TypeId == valueIds.TypeId,
            "Values.EnumStringProperties",
            valueIds,
            obj);

        return enumStringProperty.Value?.Description;
    }

    private static T FindProperty<T>(
        IEnumerable<T>? source, 
        Func<T, bool> predicate, string sourceName, 
        IIdentifiableObject ids,
        INamedObject obj)
    {
        var sourceNull = source == null;
        var item = sourceNull ? default : source.FirstOrDefault(predicate);
        if (sourceNull || item == null)
        {
            throw new InvalidDataException($"Не удалось найти необходимое значение из списка {sourceName} " +
                                           $"с ObjectId '{ids.ObjectId}' и TypeId '{ids.TypeId}' " +
                                           $"для объекта с ObjectId '{obj.ObjectId}' и TypeId '{obj.TypeId}' с именем '{obj.Name}'");
        }

        return item;
    }
}
