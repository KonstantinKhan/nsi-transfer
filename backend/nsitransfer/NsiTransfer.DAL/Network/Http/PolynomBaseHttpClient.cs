using Ascon.Polynom.Web.Api.Data.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NsiTransfer.DAL.Network.Http;

internal abstract class PolynomBaseHttpClient
{
    private static readonly JsonSerializerOptions LogJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;

    protected PolynomBaseHttpClient(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient;
        Logger = logger;
    }

    protected Task<HttpResponseMessage> GetAsyncCore(string endpoint, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        return ExecuteAsync("GetAsync", request, cancellationToken);
    }


    protected Task<HttpResponseMessage> PostAsyncCore<T>(string endpoint, T data, CancellationToken cancellationToken = default)
    {
        var request = CreateRequestMessageWithJsonContent(HttpMethod.Post, endpoint, data);
        return ExecuteAsync("PostAsync", request, cancellationToken, typeof(T).Name);
    }


    protected Task<HttpResponseMessage> DeleteAsyncCore(string endpoint, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        return ExecuteAsync("DeleteAsync", request, cancellationToken);
    }

    protected Task<HttpResponseMessage> PutAsyncCore<T>(string endpoint, T data, CancellationToken cancellationToken = default)
    {
        var request = CreateRequestMessageWithJsonContent(HttpMethod.Put, endpoint, data);
        return ExecuteAsync("PutAsync", request, cancellationToken, typeof(T).Name);
    }
    
    protected Task<HttpResponseMessage> PatchAsyncCore(string endpoint, HttpContent httpContent, CancellationToken cancellationToken = default)
    {
        var request = CreateRequestMessageSimple(HttpMethod.Patch, endpoint, httpContent);
        return ExecuteAsync("PatchAsync", request, cancellationToken);
    }

    protected Task<HttpResponseMessage> SendAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken = default)
        => ExecuteAsync("SendAsync", request, cancellationToken);


    private HttpRequestMessage CreateRequestMessageWithJsonContent<T>(HttpMethod method, string endpoint, T data)
        => new (method, endpoint)
        {
            Content = JsonContent.Create(data, options: PolynomJsonOptions.Options)
        };

    private HttpRequestMessage CreateRequestMessageSimple(HttpMethod method, string endpoint, HttpContent content) => new (method, endpoint) { Content = content };


    private async Task<HttpResponseMessage> ExecuteAsync(
        string operationName,
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        string? payloadType = null)
    {
        var endpoint = request.RequestUri?.ToString() ?? "<unknown>";

        await LogRequestAsync(operationName, request, endpoint, payloadType, cancellationToken);

        var response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await ReadResponseContentForLogAsync(response, cancellationToken);
            if (string.IsNullOrWhiteSpace(payloadType))
            {
                Logger.LogWarning(
                    "[{Operation}] Запрос на {Endpoint} не удался с кодом статуса {StatusCode}. ReasonPhrase: {ReasonPhrase}. Ответ: {ErrorContent}",
                    operationName,
                    endpoint,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    errorContent);
            }
            else
            {
                Logger.LogWarning(
                    "[{Operation}<{Type}>] Запрос на {Endpoint} не удался с кодом статуса {StatusCode}. ReasonPhrase: {ReasonPhrase}. Ответ: {ErrorContent}",
                    operationName,
                    payloadType,
                    endpoint,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    errorContent);
            }

            return response;
        }

        if (string.IsNullOrWhiteSpace(payloadType))
        {
            Logger.LogInformation("[{Operation}] Успешно выполнен запрос на: {Endpoint}", operationName, endpoint);
        }
        else
        {
            Logger.LogInformation("[{Operation}<{Type}>] Успешно выполнен запрос на: {Endpoint}", operationName, payloadType, endpoint);
        }

        return response;
    }

    private static async Task<string> ReadResponseContentForLogAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            return string.Empty;
        }

        var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return rawContent;
        }

        if (!IsJsonContent(response.Content.Headers.ContentType))
        {
            return rawContent;
        }

        try
        {
            using var document = JsonDocument.Parse(rawContent);
            return JsonSerializer.Serialize(document.RootElement, LogJsonOptions);
        }
        catch (JsonException)
        {
            return rawContent;
        }
    }

    private static bool IsJsonContent(MediaTypeHeaderValue? contentType)
        => contentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private async Task LogRequestAsync(
        string operationName,
        HttpRequestMessage request,
        string endpoint,
        string? payloadType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payloadType))
        {
            Logger.LogInformation(
                "[{Operation}] Отправка запроса: {Method} {Endpoint}",
                operationName,
                request.Method,
                endpoint);
        }
        else
        {
            Logger.LogInformation(
                "[{Operation}<{Type}>] Отправка запроса: {Method} {Endpoint}",
                operationName,
                payloadType,
                request.Method,
                endpoint);
        }

        var headerLines = new List<string>();

        foreach (var header in HttpClient.DefaultRequestHeaders)
        {
            headerLines.Add($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        foreach (var header in request.Headers)
        {
            headerLines.Add($"{header.Key}: {string.Join(", ", header.Value)}");
        }

        if (request.Content != null)
        {
            foreach (var header in request.Content.Headers)
            {
                headerLines.Add($"{header.Key}: {string.Join(", ", header.Value)}");
            }
        }

        if (headerLines.Count == 0)
        {
            Logger.LogDebug("[{Operation}] Заголовки запроса отсутствуют", operationName);
        }
        else
        {
            Logger.LogDebug("[{Operation}] Заголовки запроса:{NewLine}{Headers}",
                operationName,
                Environment.NewLine,
                string.Join(Environment.NewLine, headerLines));
        }

        if (request.Content == null)
        {
            Logger.LogDebug("[{Operation}] Тело запроса отсутствует", operationName);
            return;
        }

        try
        {
            //await request.Content.LoadIntoBufferAsync(cancellationToken);
            var body = await request.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
            {
                Logger.LogDebug("[{Operation}] Тело запроса пустое", operationName);
            }
            else
            {
                Logger.LogDebug(
                    "[{Operation}] Тело запроса:{NewLine}{Body}",
                    operationName,
                    Environment.NewLine,
                    body);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "[{Operation}] Не удалось прочитать тело запроса", operationName);
        }
    }
}
