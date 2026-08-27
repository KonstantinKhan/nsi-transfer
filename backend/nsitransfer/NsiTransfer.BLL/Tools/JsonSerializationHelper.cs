using System.Text.Encodings.Web;
using System.Text.Json;
using Ascon.Polynom.Web.Api.Data.Json;

namespace NsiTransfer.BLL.Tools;

/// <summary>
/// Утилита для сериализации объектов в JSON с поддержкой чтения кириллицы и других не-ASCII символов.
/// </summary>
public static class JsonSerializationHelper
{
    /// <summary>
    /// Кэшированные опции JSON сериализации с отступами и поддержкой Cyrillic символов.
    /// </summary>
    private static readonly JsonSerializerOptions CachedIndentedOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Кэшированные опции JSON сериализации без отступов и с поддержкой Cyrillic символов.
    /// </summary>
    private static readonly JsonSerializerOptions CachedCompactOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Форматирует объект в JSON строку с красивым отступом и кириллицей.
    /// Использует PolynomJsonOptions для полной сериализации всех свойств.
    /// </summary>
    /// <param name="data">Объект для сериализации</param>
    /// <returns>Отформатированная JSON строка с читаемыми символами</returns>
    public static string SerializeToJsonPolynomOptions<T>(T? data)
    {
        if (data == null)
            return "(пусто)";

        try
        {
            // Сериализуем с PolynomJsonOptions для полной сериализации всех свойств, включая InnerObjects
            var json = JsonSerializer.Serialize(data, PolynomJsonOptions.Options);
            
            // Переформатируем JSON с красивым отступом и UnsafeRelaxedJsonEscaping для кириллицы
            var jsonDocument = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(jsonDocument.RootElement, CachedIndentedOptions);
        }
        catch (Exception ex)
        {
            return $"Ошибка сериализации: {ex.Message}";
        }
    }

    /// <summary>
    /// Сериализует объект в JSON строку с кириллицей (без экранирования Unicode).
    /// Не использует PolynomJsonOptions
    /// </summary>
    /// <param name="data">Объект для сериализации</param>
    /// <param name="writeIndented">Добавлять ли отступы для читаемости</param>
    /// <returns>JSON строка</returns>
    public static string SerializeToJsonSimple(object? data, bool writeIndented = false)
    {
        if (data == null)
            return "null";

        try
        {
            var options = writeIndented ? CachedIndentedOptions : CachedCompactOptions;
            return JsonSerializer.Serialize(data, options);
        }
        catch (Exception ex)
        {
            return $"Ошибка сериализации: {ex.Message}";
        }
    }
}
