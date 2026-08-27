using Microsoft.Extensions.Options;
using NsiTransfer.Contract.ConfigModels;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace NsiTransfer.AppConfig;

/// <summary>
/// Рекурсивно валидирует AppConfiguration и все вложенные модели через DataAnnotations.
/// Встроенный ValidateDataAnnotations() из Microsoft.Extensions.Options проверяет только
/// верхний уровень, поэтому атрибуты внутри PolynomConfig, RabbitMqRetryParams и т.д.
/// без этого класса просто не сработают.
/// </summary>
public sealed class AppConfigurationValidator : IValidateOptions<AppConfiguration>
{
    public ValidateOptionsResult Validate(string? name, AppConfiguration options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Конфигурация не задана.");
        }

        var failures = new List<string>();
        ValidateRecursively(options, nameof(AppConfiguration), new HashSet<object>(ReferenceEqualityComparer.Instance), failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateRecursively(object instance, string path, HashSet<object> visited, List<string> failures)
    {
        if (!visited.Add(instance))
        {
            return; // защита от циклических ссылок на случай будущих изменений модели
        }

        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);

        foreach (var result in results)
        {
            var member = result.MemberNames.Any() ? string.Join(", ", result.MemberNames) : "значение";
            failures.Add($"{path}.{member}: {result.ErrorMessage}");
        }

        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;

            var value = property.GetValue(instance);
            if (value is null) continue;

            if (property.PropertyType.IsPrimitive || property.PropertyType.IsEnum || value is string) continue;

            if (value is System.Collections.IEnumerable enumerable)
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    if (item is not null && !item.GetType().IsPrimitive && item is not string)
                        ValidateRecursively(item, $"{path}.{property.Name}[{index}]", visited, failures);
                    index++;
                }
                continue;
            }

            if (property.PropertyType.IsClass)
            {
                ValidateRecursively(value, $"{path}.{property.Name}", visited, failures);
            }
        }
    }
}
