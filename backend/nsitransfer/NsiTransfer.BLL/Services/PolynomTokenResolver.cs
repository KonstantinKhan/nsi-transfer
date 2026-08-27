using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Interfaces;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.BLL.Services;

public class PolynomTokenResolver : IPolynomTokenResolver
{
    private readonly SessionManager _sessionManager;
    private readonly IOptionsMonitor<PolynomAuthCreds> _optionsMonitor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PolynomTokenResolver(
        SessionManager sessionManager,
        IOptionsMonitor<PolynomAuthCreds> optionsMonitor,
        IHttpContextAccessor httpContextAccessor)
    {
        _sessionManager = sessionManager;
        _optionsMonitor = optionsMonitor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<string>> ResolveAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // Если есть HttpContext, сначала пытаемся получить токен пользователя.
            // Это сработает для аутентифицированных запросов.
            var accessToken = httpContext.User.FindFirst(AuthConstants.AccessToken)?.Value;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                return Result<string>.Success(accessToken);
            }
        }

        // Если мы здесь, значит это либо фоновый процесс (HttpContext is null),
        // либо неаутентифицированный запрос из контроллера (токен не найден).
        // В обоих случаях используем сервисный аккаунт.
        var options = _optionsMonitor.CurrentValue;

        if (IsCredsNonValid(options.Username, options.Password))
        {
            var reason = httpContext == null
                ? "из фонового сервиса"
                : "для анонимного пользователя";
            return Result<string>.Failure($"Запрос в API Полином {reason} пропущен, так как учетные данные сервисного аккаунта не настроены.");
        }

        return await _sessionManager.EnsureAccessTokenValidityAsync(options.Username, options.Password, cancellationToken);
    }

    private static bool IsCredsNonValid(string username, string password)
    {
        return string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrEmpty(username)      || string.IsNullOrEmpty(password);
    }
}