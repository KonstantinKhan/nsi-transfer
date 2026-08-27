using Microsoft.AspNetCore.Http;

namespace NsiTransfer.DAL.Routes;

/// <summary>
/// Набор адресов конечных точек web API.
/// </summary>
public static class ApiRoutes
{
    public static string Path(string value, int version = 1)
    {
        return $"/api/v{version}{value}";
    }


    /// <summary>
    /// Конечная точка списка всех справочников.
    /// </summary>
    public static string ReferenceAll => Path("/reference/get-all");

    /// <summary>
    /// Конечная точка создания справочника.
    /// </summary>
    public static string ReferenceCreate => Path("/reference/create");

    /// <summary>
    /// Конечная точка получения объекта справочника по строке расположения.
    /// </summary>
    public static string ElementByLocation => Path("/element/get-by-location");

    /// <summary>
    /// Конечная точка получения каталога документов справочника.
    /// </summary>
    public static string DocumentCatalogByReference => Path("/document-catalog/get-by-reference");

    /// <summary>
    /// Конечная точка получения списка групп документов каталога.
    /// </summary>
    public static string DocumentGroupByCatalog => Path("/document-group/get-by-catalog");

    /// <summary>
    /// Конечная точка создания группы документов каталога.
    /// </summary>
    public static string DocumentGroupCreate => Path("/document-group/create-document-group");

    /// <summary>
    /// Конечная точка получения списка документов группы.
    /// </summary>
    public static string DocumentByGroup => Path("/document/get-by-document-group");

    /// <summary>
    /// Конечная точка создания документа.
    /// </summary>
    public static string DocumentCreate => Path("/document/create-document");

    /// <summary>
    /// Конечная точка получения свойств по расположению.
    /// </summary>
    public static string PropertyOwnerByLocation => Path("/property-owner/get-by-location");

    /// <summary>
    /// Конечная точка загрузки временного файла в хранилище.
    /// </summary>
    public static string FileUpload => Path("/file/upload");

    /// <summary>
    /// Конечная точка добавления файла документу.
    /// </summary>
    public static string AddToDocument => Path("/file/add-to-document");

    /// <summary>
    /// Конечная точка получения имени временного файла для скачивания.
    /// </summary>
    public static string FileContentById => Path("/file/get-by-id");

    /// <summary>
    /// Конечная точка загрузки значения бинарного свойства в хранилище временных файлов.
    /// </summary>
    public static string UploadBinaryPropertyValue => Path("/property-owner/upload-binary-property-value");

    /// <summary>
    /// Конечная точка установки значений свойств.
    /// </summary>
    public static string SetPropertyValues => Path("/property-owner/set-property-values");

    /// <summary>
    /// Конечная точка скачивания значения свойства.
    /// </summary>
    public static string GetPropertiesByUid => Path("/property-owner/get-by-uid");

    /// <summary>
    /// Конечная точка скачивания значения свойства.
    /// </summary>
    public static string GetBinaryPropertyValue => Path("/property-owner/get-binary-property-value");

    /// <summary>
    /// Конечная точка получения всех понятий со свойствами некоторого владельца свойств (объекта)
    /// </summary>
    public static string GetPropertiesOfPropertyOwner => Path("/property-owner/get-properties");

    /// <summary>
    /// Конечная точка поиска по свойствам.
    /// </summary>
    public static string SearchPropertyQuery => Path("/search/execute-property-search");

    public static string MeasureEntity => Path("/measure-entity/get-all");

    public static string MeasureUnit => Path("/measure-unit/get-all");

    public static string MeasureUnitsUnitsByEntityId => Path("/measure-unit/get-by-entity");

    public static string Concept => Path("/concept/get-all");

    public static string ConceptPropertySource => Path("/concept-property-source/get-by-concept");

    public static string ConceptPropertySourceById => Path("/concept-property-source/get-by-id");

    public static string ConceptPropertySourceByAbsoluteCode => Path("/concept-property-source/get-by-absolute-code");

    public static string GetClassification => Path("/tree/get-classification");

    public static string GetClassificationNodeChildren => Path("/tree/get-classification-node-children");

    public static string GetPropertyDefinitionByAbsoluteCode => Path("/property-definition/get-by-absolute-code");

    public static string GetParentGroups => Path("/classification-object/get-parent-groups");

    /// <summary>
    /// Конечная точка получения групп, находящихся внутри указанной группы элементов.
    /// </summary>
    public static string GetGroupsInsideElementGroup => Path("/element-group/get-by-group");
}
