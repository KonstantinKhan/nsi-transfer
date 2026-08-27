using Microsoft.Extensions.DependencyInjection;
using NsiTransfer.DAL.Interfaces.Http;

namespace NsiTransfer.DAL.Network.Http;

internal class PolynomHttpClientFactory : IPolynomHttpClientFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PolynomHttpClientFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPolynomHttpClient Create(string clientKey)
    {
        return clientKey switch
        {
            PolynomHttpClientKeys.Auth => _serviceProvider.GetRequiredService<PolynomAuthHttpClient>(),
            PolynomHttpClientKeys.Api => _serviceProvider.GetRequiredService<PolynomApiHttpClient>(),
            _ => throw new InvalidOperationException($"Polynom http client key '{clientKey}' is not registered.")
        };
    }
}
