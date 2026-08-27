namespace NsiTransfer.DAL.Interfaces.Http;

internal interface IPolynomHttpClient
{
    string ClientKey { get; }

    Task<HttpResponseMessage> GetAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync<T>(string endpoint, T data, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsync<T>(string endpoint, T data, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PatchAsync(string endpoint, HttpContent httpContent, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);
}

internal static class PolynomHttpClientKeys
{
    public const string Auth = "auth";
    public const string Api = "api";
}
