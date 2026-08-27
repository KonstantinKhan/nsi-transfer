using System.Net;
using Microsoft.Extensions.Logging;
using NsiTransfer.Contract;
using NsiTransfer.Contract.Interfaces;

namespace NsiTransfer.DAL.Network.Http;

internal class PolynomSessionHttpClientHandler : DelegatingHandler
{
    private readonly IPolynomTokenResolver _tokenResolver;
    private readonly ILogger<PolynomSessionHttpClientHandler> _logger;

    public PolynomSessionHttpClientHandler(IPolynomTokenResolver tokenResolver, ILogger<PolynomSessionHttpClientHandler> logger)
    {
        _tokenResolver = tokenResolver;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var actualAccessTokenResult = await _tokenResolver.ResolveAccessTokenAsync(cancellationToken);
        if (!actualAccessTokenResult.IsSuccess)
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                ReasonPhrase = $"Не удалось получить access token для выполнения запроса на API Полином.",
                Content = new StringContent($" Ошибка: {actualAccessTokenResult.ErrorMessage}")
            };
        }

        request.Headers.TryAddWithoutValidation(AuthConstants.AuthorizationHeaderName, $"{AuthConstants.BearerTypeName} {actualAccessTokenResult.Data}");

        _logger.LogDebug("Добавлен заголовок Authorization в запрос: {Uri}", request.RequestUri);

        return await base.SendAsync(request, cancellationToken);
    }
}
