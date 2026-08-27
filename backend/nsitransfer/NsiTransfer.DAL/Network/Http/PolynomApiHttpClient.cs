using Microsoft.Extensions.Logging;
using NsiTransfer.DAL.Interfaces.Http;

namespace NsiTransfer.DAL.Network.Http;

internal class PolynomApiHttpClient : PolynomBaseHttpClient, IPolynomHttpClient
{
    public string ClientKey => PolynomHttpClientKeys.Api;

    public PolynomApiHttpClient(HttpClient httpClient, ILogger<PolynomApiHttpClient> logger)
        : base(httpClient, logger) { }

    public Task<HttpResponseMessage> GetAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return GetAsyncCore(endpoint, cancellationToken);
    }

    public Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data, CancellationToken cancellationToken = default)
    {
        return PostAsyncCore(endpoint, data, cancellationToken);
    }

    public Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return DeleteAsyncCore(endpoint, cancellationToken);
    }

    public Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data, CancellationToken cancellationToken = default)
    {
        return PutAsyncCore(endpoint, data, cancellationToken);
    }

    public Task<HttpResponseMessage> PatchAsync(string endpoint, HttpContent httpContent, CancellationToken cancellationToken = default)
    {
        return PatchAsyncCore(endpoint, httpContent, cancellationToken);
    }

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return SendAsyncCore(request, cancellationToken);
    }
}
