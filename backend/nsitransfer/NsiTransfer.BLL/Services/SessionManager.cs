using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Requests.Login;
using Ascon.Polynom.Web.Api.Data.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NsiTransfer.BLL.Tools;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Interfaces.Http;

namespace NsiTransfer.BLL.Services;

public class SessionManager
{
    private readonly IPolynomAuthHttpRepository _authRepository;
    private readonly SessionStore _sessionStore;
    private readonly IOptionsMonitor<PolynomConfig> _optionsMonitor;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(
        IPolynomAuthHttpRepository authRepository,
        SessionStore sessionStore,
        IOptionsMonitor<PolynomConfig> optionsMonitor,
        ILogger<SessionManager> logger)
    {
        _authRepository = authRepository;
        _sessionStore = sessionStore;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<Result<string>> EnsureAccessTokenValidityAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var key = new UserCredentialsKey(username, password);
        if (_sessionStore.TryGet(key, out var existingToken) && !existingToken.IsExpired)
        {
            return Result<string>.Success(existingToken.AccessToken);
        }


        return await AuthInPolynom(existingToken, username, password, cancellationToken)
            .OnFailureAsync((string error) => 
            {
                _logger.LogError("Не удалось обновить access token для пользователя {Username} или аутентифицироваться заново. Ошибка: {Error}", username, error);
                _sessionStore.TryRemove(key);
            })
            .MapAsync(tokenResposne =>
            {
                var mapped = MapToken(tokenResposne);
                _sessionStore.Set(key, mapped);
                return mapped.AccessToken;
            });
    }

    private SignInRequest CreateSignInRequest(string username, string password, ClientTypes clientType, StorageDefinitionResponse storage)
    {
        return new SignInRequest
        {
            Login = username,
            Password = password,
            StorageId = storage.StorageId.ToString(),
            ClientType = clientType
        };
    }

    private async Task<Result<AuthResponse>> AuthInPolynom(PolynomAuthTokenModel existingToken, string username, string password, CancellationToken cancellationToken)
    {
        var signInRequestCustom = new SignInRequestCustom(username, password);

        // Если токен есть, пробуем обновить, если нет - аутентифицируемся
        var refreshToken = existingToken?.RefreshToken;
        if (refreshToken == null)
        {
            var signInResult = await _authRepository.SignInAsync(signInRequestCustom, cancellationToken);
            return signInResult;
        }

        var refreshedTokenResult = await _authRepository.RefreshTokenOrSignInAsync(refreshToken, signInRequestCustom, cancellationToken);
        return refreshedTokenResult;
    }

    private static PolynomAuthTokenModel MapToken(AuthResponse authResponse)
    {
        return new PolynomAuthTokenModel
        {
            AccessToken = authResponse.AccessToken,
            ExpiresIn = authResponse.ExpiresIn,
            RefreshToken = authResponse.RefreshToken,
            TokenType = authResponse.TokenType
        };
    }
}
