using Ascon.Polynom.Web.Api.Data.Requests.Login;
using Ascon.Polynom.Web.Api.Data.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NsiTransfer.Contract;
using NsiTransfer.Contract.Models;
using NsiTransfer.DAL.Interfaces.Http;
using Swashbuckle.AspNetCore.Annotations;

namespace NsiTransfer.Presentation.Controllers;

[Route("api/auth")]
[ApiController]
[SwaggerTag("Методы для аутентификации в API Полинома и работы с сессией Полинома")]
public class AuthController : ControllerBase
{
    private readonly IPolynomAuthHttpRepository _authRepository;
    private readonly ILogger<AuthController> _logger;


    public AuthController(
        IPolynomAuthHttpRepository authRepository,
        ILogger<AuthController> logger)
    {
        _authRepository = authRepository;
        _logger = logger;
    }

    /// <summary>
    /// Получить список всех доступных хранилищ из API Полином
    /// </summary>
    [HttpGet("storages")]
    public async Task<IActionResult> GetStorages()
    {
        _logger.LogInformation("Получение списка хранилищ из API Полином");

        var result = await _authRepository.GetStoragesAsync(HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            _logger.LogError("Ошибка при получении списка хранилищ: {Error}", result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { storages = result.Data });
    }

    /// <summary>
    /// Аутентификация пользователя в Polynom API
    /// Устанавливает токены в HttpOnly cookies и возвращает метаданные клиенту
    /// </summary>
    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequestCustom request)
    {
        if (request == null)
        {
            _logger.LogWarning("Получен пустой объект SignInRequest");
            return BadRequest(new { error = "SignInRequest не может быть пустым" });
        }

        _logger.LogInformation("Попытка аутентификации для пользователя {Login}", request.Login);

        var result = await _authRepository.SignInAsync(request, HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            _logger.LogError("Ошибка аутентификации для пользователя {Login}: {Error}", request.Login, result.ErrorMessage);
            return Unauthorized(new { error = result.ErrorMessage });
        }

        var authResponse = result.Data!;
        _logger.LogInformation("Успешная аутентификация для пользователя {Login}", request.Login);

        var userInfo = await _authRepository.GetUserInfo(authResponse.AccessToken, HttpContext.RequestAborted);
        if (!userInfo.IsSuccess)
        {
            _logger.LogError("Ошибка получения информации о пользователе {Login}: {Error}", request.Login, result.ErrorMessage);
            return Unauthorized(new {error =  userInfo.ErrorMessage});
        }

        SetAuthCookies(authResponse);

        return Ok(new
        {
            isAuthenticated = true,
            firstName = userInfo.Data!.FirstName,
            lastName = userInfo.Data.LastName,
            patronymic = userInfo.Data.Patronymic,
            login = userInfo.Data.Login,
            email = userInfo.Data.Email,
            tokenType = authResponse.TokenType,
            expiresIn = authResponse.ExpiresIn,
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(authResponse.ExpiresIn).ToUnixTimeMilliseconds()
        });
    }

    /// <summary>
    /// Выход из учётной записи
    /// Требует access token в Authorization заголовке или cookie
    /// </summary>
    [HttpDelete("signout")]
    [Authorize]
    public async Task<IActionResult> SignOut()
    {
        var token = Request.Cookies[AuthConstants.AccessTokenCookieName];

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("SignOut: отсутствует access token");
            return BadRequest(new { error = "Access token отсутствует" });
        }

        _logger.LogInformation("Попытка выхода из учётной записи");


        var result = await _authRepository.SignOutAsync(token, HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            _logger.LogError("Ошибка при выходе из учётной записи: {Error}", result.ErrorMessage);
            return BadRequest(new { error = result.ErrorMessage });
        }

        // Очищаем cookies
        ClearAuthCookies();

        _logger.LogInformation("Успешный выход из учётной записи");
        return Ok(new { message = "Успешный выход из учётной записи" });
    }

    /// <summary>
    /// Обновить access token используя refresh token из cookie
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken()
    {
        // Читаем refresh token из cookie
        var refreshToken = Request.Cookies[AuthConstants.RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Refresh token отсутствует в cookies");
            return Unauthorized(new { error = "Refresh token отсутствует" });
        }

        _logger.LogInformation("Попытка обновления access token");

        var result = await _authRepository.RefreshTokenAsync(refreshToken, HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            _logger.LogError("Ошибка при обновлении token: {Error}", result.ErrorMessage);
            // Очищаем cookies при ошибке refresh
            ClearAuthCookies();
            return Unauthorized(new { error = result.ErrorMessage });
        }

        var authResponse = result.Data!;
        _logger.LogInformation("Access token успешно обновлён");

        var userInfo = await _authRepository.GetUserInfo(authResponse.AccessToken, HttpContext.RequestAborted);
        if (!userInfo.IsSuccess)
        {
            _logger.LogError("Ошибка получения информации о пользователе: {Error}", result.ErrorMessage);
            return Unauthorized(new { error = userInfo.ErrorMessage });
        }

        SetAuthCookies(authResponse);

        return Ok(new
        {
            isAuthenticated = true,
            firstName = userInfo.Data!.FirstName,
            lastName = userInfo.Data.LastName,
            patronymic = userInfo.Data.Patronymic,
            login = userInfo.Data.Login,
            email = userInfo.Data.Email,
            tokenType = authResponse.TokenType,
            expiresIn = authResponse.ExpiresIn,
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(authResponse.ExpiresIn).ToUnixTimeMilliseconds()
        });
    }

    /// <summary>
    /// Устанавливает токены авторизации в HttpOnly cookies
    /// </summary>
    private void SetAuthCookies(AuthResponse authResponse)
    {
        var baseOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax, // Баланс между безопасностью и функциональностью
            Path = "/"
        };

        // Access token cookie
        var accessTokenOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddSeconds(authResponse.ExpiresIn)
        };
        Response.Cookies.Append(AuthConstants.AccessTokenCookieName, authResponse.AccessToken, accessTokenOptions);

        // Refresh token cookie (обычно живёт дольше)
        var refreshTokenOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30) // Refresh живёт 30 дней
        };
        Response.Cookies.Append(AuthConstants.RefreshTokenCookieName, authResponse.RefreshToken, refreshTokenOptions);

        // ExpiresAt в обычной cookie (без HttpOnly) для клиентского использования
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(authResponse.ExpiresIn).ToUnixTimeMilliseconds();
        var expiresAtOptions = new CookieOptions
        {
            HttpOnly = false, // Можно читать из JavaScript
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddSeconds(authResponse.ExpiresIn)
        };
        Response.Cookies.Append(AuthConstants.ExpiresAtCookieName, expiresAt.ToString(), expiresAtOptions);

        _logger.LogDebug("Cookies установлены: access token expires in {ExpiresIn} seconds", authResponse.ExpiresIn);
    }

    /// <summary>
    /// Очищает все cookies авторизации
    /// </summary>
    private void ClearAuthCookies()
    {
        var expiredOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        Response.Cookies.Append(AuthConstants.AccessTokenCookieName, "", expiredOptions);
        Response.Cookies.Append(AuthConstants.RefreshTokenCookieName, "", expiredOptions);

        var expiredPublicOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };
        Response.Cookies.Append(AuthConstants.ExpiresAtCookieName, "", expiredPublicOptions);

        _logger.LogDebug("Cookies авторизации очищены");
    }

    /// <summary>
    /// Вспомогательный метод для извлечения Bearer token из Authorization заголовка
    /// </summary>
    private static string ExtractBearerToken(string authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return null;
        }

        const string bearerScheme = "Bearer ";
        if (authorizationHeader.StartsWith(bearerScheme, StringComparison.OrdinalIgnoreCase))
        {
            return authorizationHeader[bearerScheme.Length..];
        }

        return null;
    }
}

/// <summary>
/// Модель запроса для обновления токена (больше не используется, так как токен в cookie)
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; }
}
