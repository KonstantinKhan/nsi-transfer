using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NsiTransfer.Contract;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace NsiTransfer.Authentication;

/// <summary>
/// Authentication handler, который извлекает access token
/// из HttpOnly cookie или из заголовка Authorization: Bearer.
/// 
/// Приоритет источников:
/// 1. Cookie "auth_access_token" (основной способ для веб-клиента)
/// 2. Заголовок Authorization: Bearer (для обратной совместимости)
/// </summary>
public class SimpleBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public SimpleBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // 1. Пытаемся извлечь токен из HttpOnly cookie
            var token = Request.Cookies[AuthConstants.AccessTokenCookieName];

            // 2. Если cookie нет — пробуем заголовок Authorization (fallback)
            if (string.IsNullOrWhiteSpace(token))
            {
                token = ExtractTokenFromHeader();
            }

            // 3. Если токен так и не нашли — нет результата (не ошибка, а факт отсутствия)
            if (string.IsNullOrWhiteSpace(token))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // 4. Формируем ClaimsPrincipal
            var claims = new[]
            {
                new Claim(AuthConstants.AccessToken, token),
                // Добавляем claim с источником токена — полезно для логирования/отладки
                new Claim("token_source", Request.Cookies.ContainsKey(AuthConstants.AccessTokenCookieName) ? "cookie" : "header")
            };

            var identity = new ClaimsIdentity(claims, AuthConstants.AuthenticationSchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthConstants.AuthenticationSchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Исключение при аутентификации пользователя");
            return Task.FromResult(AuthenticateResult.Fail(ex));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        return Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized",
            message = "Требуется аутентификация. Access token должен быть передан " +
                      "через cookie 'auth_access_token' или заголовок 'Authorization: Bearer <token>'"
        });
    }

    /// <summary>
    /// Извлекает Bearer-токен из заголовка Authorization.
    /// Возвращает null, если заголовок отсутствует или имеет неверный формат.
    /// </summary>
    private string? ExtractTokenFromHeader()
    {
        if (!Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            return null;
        }

        var authHeader = Request.Headers.Authorization.ToString();

        if (!authHeader.StartsWith(AuthConstants.BearerTypeName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader[AuthConstants.BearerTypeName.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
