using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Security;
using Ascon.Polynom.Web.Api.Data.Json;
using Ascon.Polynom.Web.Api.Data.Models.Security;
using Ascon.Polynom.Web.Api.Data.Requests.Login;
using Ascon.Polynom.Web.Api.Data.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NsiTransfer.Contract;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Interfaces.Http;
using NsiTransfer.DAL.Routes;
using System.Net.Http.Json;
using System.Text;

namespace NsiTransfer.DAL.Repositories.Network;

internal class PolynomAuthHttpRepository : BaseHttpRepository, IPolynomAuthHttpRepository
{
    private readonly IPolynomHttpClient _authHttpClient;
    private readonly IOptionsMonitor<PolynomConfig> _optionsMonitor;
    private readonly ILogger<PolynomAuthHttpRepository> _logger;

    public PolynomAuthHttpRepository(
        IPolynomHttpClientFactory httpClientFactory,
        IOptionsMonitor<PolynomConfig> optionsMonitor,
        ILogger<PolynomAuthHttpRepository> logger) : base(logger)
    {
        _authHttpClient = httpClientFactory.Create(PolynomHttpClientKeys.Auth);
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<Result<List<StorageDefinitionResponse>>> GetStoragesAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendRequestAsync(() => _authHttpClient.GetAsync(ApiLoginRoutes.StorageDefinitions, cancellationToken), cancellationToken);
        if (!result.IsSuccess) return $"Не удалось отправить запрос на endpoint {ApiLoginRoutes.StorageDefinitions}. Ошибка: {result.ErrorMessage}";
        
        if (!result.Data!.IsSuccessStatusCode) return await CreateHttpErrorAsync<List<StorageDefinitionResponse>>(result.Data!, cancellationToken);

        var content = result.Data!.Content;
        var storagesModels = await content.ReadFromJsonAsync<List<StorageDefinitionResponse>>(PolynomJsonOptions.Options, cancellationToken);
        return storagesModels switch
        {
            null => "Вероятно от сервера получен пустой ответ, не содержащий данных о хранилищах, или произошла ошибка при десериализации. Рекомендуется проверить корректность работы сервера и его ответов.",
            _ => storagesModels
        };
    }

    public async Task<Result<AuthResponse>> SignInAsync(SignInRequestCustom signInRequestCustom, CancellationToken cancellationToken = default)
    {
        if (signInRequestCustom == null)
        {
            return $"Не задан объект {nameof(signInRequestCustom)} для выполнения аутентификации.";
        }

        var storageResult = await GetStoragesAsync(cancellationToken);
        if (!storageResult.IsSuccess || storageResult.Data.Count == 0)
        {
            var errorMessage = "Не удалось получить данные о хранилищах перед попыткой аутентификации в Полином. Ошибка: " + storageResult.ErrorMessage;
            _logger.LogError(errorMessage);
            return errorMessage;
        }

        var storage = storageResult.Data.FirstOrDefault(s => s.DisplayName.Equals(_optionsMonitor.CurrentValue.DbName, StringComparison.OrdinalIgnoreCase));
        if (storage == null) return $"Не найдено хранилище с именем {_optionsMonitor.CurrentValue.DbName} среди полученных данных о хранлищах от API Полинома перед попыткой аутентификации в Полином";

        var signInRequestPolynom = new SignInRequest
        {
            ClientType = Ascon.Polynom.Web.Api.Data.Interfaces.Enums.ClientTypes.Client,
            ModuleName = "",
            Login = signInRequestCustom.Login,
            Password = signInRequestCustom.Password,
            StorageId = storage.StorageId.ToString()
        };

        var result = await SendRequestAsync(() => _authHttpClient.PostAsync(ApiLoginRoutes.SignIn, signInRequestPolynom, cancellationToken), cancellationToken);
        if (!result.IsSuccess) return $"Не удалось отправить запрос на endpoint {ApiLoginRoutes.SignIn}. Ошибка: {result.ErrorMessage}";
        
        if (!result.Data!.IsSuccessStatusCode) return await CreateHttpErrorAsync<AuthResponse>(result.Data!, cancellationToken);

        var content = result.Data!.Content;
        var authResponse = await content.ReadFromJsonAsync<AuthResponse>(PolynomJsonOptions.Options, cancellationToken);

        return authResponse switch
        {
            null => "Ошибка при десериализации ответа авторизации. Вероятно не удалось опознать модель данных, полученных от сервера.",
            _ => authResponse
        };
    }

    public async Task<Result> SignOutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return "Не задан Access Token для выполнения Sign Out.";
        }

        var authHeader = $"Bearer {accessToken}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, ApiLoginRoutes.SignOut);
        request.Headers.TryAddWithoutValidation(AuthConstants.AuthorizationHeaderName, authHeader);

        var result = await SendRequestAsync(() => _authHttpClient.SendAsync(request, cancellationToken), cancellationToken);
        if (!result.IsSuccess) return $"Не удалось отправить запрос на endpoint {ApiLoginRoutes.SignOut}. Ошибка: {result.ErrorMessage}";

        if (!result.Data!.IsSuccessStatusCode) return await CreateHttpErrorAsync(result.Data!, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return "Не задан Refresh Token для выполнения обновления токена.";
        }

        var preparedHttpContent = new StringContent(refreshToken, Encoding.UTF8, "text/plain");

        // Оказывается для обновления токена не требуется в заголовки пихать Authorization с access token'ом, достаточно отправить сам refresh token в теле запроса
        var result = await SendRequestAsync(() => _authHttpClient.PatchAsync(ApiLoginRoutes.UpdateToken, preparedHttpContent, cancellationToken), cancellationToken);
        if (!result.IsSuccess) return $"Не удалось отправить запрос на endpoint {ApiLoginRoutes.UpdateToken}. Ошибка: {result.ErrorMessage}";

        var responseMessage = result.Data!;
        if (responseMessage.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return "Токен обновления недействителен или просрочен. Необходимо выполнить повторную аутентификацию.";
        }
        if (!responseMessage.IsSuccessStatusCode) return await CreateHttpErrorAsync<AuthResponse>(responseMessage, cancellationToken);



        var content = responseMessage.Content;
        var authResponse = await content.ReadFromJsonAsync<AuthResponse>(PolynomJsonOptions.Options, cancellationToken);

        return authResponse switch
        {
            null => "Ошибка при десериализации ответа обновления токена. Вероятно не удалось опознать модель данных, полученных от сервера.",
            _ => authResponse
        };
    }

    public async Task<Result<AuthResponse>> RefreshTokenOrSignInAsync(string refreshToken, SignInRequestCustom signInRequestCustom, CancellationToken cancellationToken = default)
    {
        var refreshResult = await RefreshTokenAsync(refreshToken, cancellationToken);
        if (refreshResult.IsSuccess) return refreshResult;

        _logger.LogWarning("Не удалось обновить токен с помощью refresh token'а. Ошибка: {Error}. Попытка повторной аутентификации...", refreshResult.ErrorMessage);
        return await SignInAsync(signInRequestCustom, cancellationToken);
    }

    public async Task<Result<IUser>> GetUserInfo(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return "Не задан Access Token для получения информации по текущему пользователю.";
        }

        var authHeader = $"Bearer {accessToken}";
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiLoginRoutes.CurrentUserInfo);
        request.Headers.TryAddWithoutValidation(AuthConstants.AuthorizationHeaderName, authHeader);

        var result = await SendRequestAsync(() => _authHttpClient.SendAsync(request, cancellationToken), cancellationToken);
        if (!result.IsSuccess) return $"Не удалось отправить запрос на endpoint {ApiLoginRoutes.CurrentUserInfo}. Ошибка: {result.ErrorMessage}";

        if (!result.Data!.IsSuccessStatusCode) return await CreateHttpErrorAsync<IUser>(result.Data!, cancellationToken);

        var content = result.Data!.Content;
        var user = await content.ReadFromJsonAsync<User>(PolynomJsonOptions.Options, cancellationToken);
        return user switch
        {
            null => "Вероятно от сервера получен пустой ответ, не содержащий данных о хранилищах, или произошла ошибка при десериализации. Рекомендуется проверить корректность работы сервера и его ответов.",
            _ => user
        };
    }
}