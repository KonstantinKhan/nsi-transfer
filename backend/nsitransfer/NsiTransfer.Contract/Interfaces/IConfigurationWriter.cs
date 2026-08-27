using NsiTransfer.Contract.ConfigModels;

namespace NsiTransfer.Contract.Interfaces;

public interface IConfigurationWriter
{
    Task UpdateAsync(Action<AppConfiguration> mutate, CancellationToken ct = default);
    Task ReplaceAsync(AppConfiguration newConfig, CancellationToken ct = default);
}
