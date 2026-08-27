using Microsoft.Extensions.Options;
using NsiTransfer.AppConfig;
using NsiTransfer.Contract.ConfigModels;
using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Extensions;

public static class ConfigurationExtensions
{
    public static T GetValidated<T>(this IConfiguration configuration, string sectionName) where T : new()
    {
        var config = configuration.GetSection(sectionName).Get<T>() ?? new T();

        var validationContext = new ValidationContext(config);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
        {
            var errors = string.Join(Environment.NewLine, validationResults.Select(r => r.ErrorMessage));
            throw new InvalidOperationException($"Некорректная конфигурация '{typeof(T).Name}':{Environment.NewLine}{errors}");
        }

        return config;
    }
}
