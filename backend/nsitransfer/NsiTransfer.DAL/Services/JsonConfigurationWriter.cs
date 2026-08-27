using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Exceptions;
using NsiTransfer.Contract.Interfaces;
using NsiTransfer.DAL.Interfaces.Db;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NsiTransfer.DAL.Services;

public sealed class JsonConfigurationWriter : IConfigurationWriter
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IValidateOptions<AppConfiguration> _validateOptions;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // 1. Не кодировать кириллицу в Unicode escape-последовательности
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,

        // 2. Использовать PascalCase (это значение по умолчанию, но можно указать явно)
        PropertyNamingPolicy = null,

        // Опционально: игнорировать регистр при десериализации
        PropertyNameCaseInsensitive = true,

        // Опционально: форматированный вывод (для отладки)
        WriteIndented = true
    };

    public JsonConfigurationWriter(
        IServiceScopeFactory scopeFactory,
        IValidateOptions<AppConfiguration> validateOptions)
    {
        _scopeFactory = scopeFactory;
        _validateOptions = validateOptions;
        _path = Environment.GetEnvironmentVariable("CONFIG_FILE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "configuration.json");
    }

    public async Task UpdateAsync(Action<AppConfiguration> mutate, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_path, ct);
            var current = JsonSerializer.Deserialize<ConfigWrapper>(json, JsonOptions)
                ?? throw new InvalidOperationException("Не удалось прочитать текущую конфигурацию");

            mutate(current.AppConfiguration);

            ValidateOrThrow(current.AppConfiguration);

            await WriteAtomicAsync(current.AppConfiguration, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReplaceAsync(AppConfiguration newConfig, CancellationToken ct = default)
    {
        ValidateOrThrow(newConfig);

        await _lock.WaitAsync(ct);
        try
        {
            await WriteAtomicAsync(newConfig, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WriteAtomicAsync(AppConfiguration config, CancellationToken ct)
    {
        var tempPath = _path + ".tmp";
        var wrapper = new ConfigWrapper(config);
        var json = JsonSerializer.Serialize(wrapper, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, _path, overwrite: true); // атомарно на Linux

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var allRefNodes = await unitOfWork.TargetReferenceNodes.GetAllAsync(cancellationToken: ct);
        if (!allRefNodes.Any(n => n.ObjectId == config.TargetReferenceNode.TargetReferenceNodeObjectId
                                && n.TypeId == config.TargetReferenceNode.TargetReferenceNodeTypeId))
        {
            var newRefNode = new Db.Entities.TargetReferenceNode
            {
                ObjectId = config.TargetReferenceNode.TargetReferenceNodeObjectId,
                TypeId = config.TargetReferenceNode.TargetReferenceNodeTypeId,
                Name = config.TargetReferenceNode.TargetReferenceNodeName
            };

            await unitOfWork.TargetReferenceNodes.AddAsync(newRefNode, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private void ValidateOrThrow(AppConfiguration config)
    {
        var result = _validateOptions.Validate(name: null, config);
        if (result.Failed)
        {
            throw new ConfigurationValidationException(result.Failures);
        }
    }

    private record class ConfigWrapper(AppConfiguration AppConfiguration);
}
