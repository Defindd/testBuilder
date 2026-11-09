using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestBuilder.Elma365;

/// <summary>
/// Клиент публичного API Elma365.
/// Все методы используют идемпотентные GET-запросы и повторяются при HTTP 429/503.
/// </summary>
public sealed class Elma365Client
{
    private const string ApiPrefix = "/pub/v1/app";
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1500),
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _authToken;
    private readonly ILogger _logger;

    /// <summary>
    /// Создаёт клиент Elma365.
    /// </summary>
    /// <param name="http">HTTP-клиент.</param>
    /// <param name="baseUrl">Базовый URL Elma365.</param>
    /// <param name="authToken">Bearer-токен.</param>
    /// <param name="logger">Логгер.</param>
    public Elma365Client(
        HttpClient http,
        string baseUrl,
        string authToken,
        ILogger<Elma365Client> logger)
        : this(http, baseUrl, authToken, (ILogger)logger)
    {
    }

    /// <summary>
    /// Создаёт клиент Elma365 с нетипизированным логгером.
    /// Используется блоками, у которых есть общий <see cref="ILogger"/> контекста выполнения.
    /// </summary>
    /// <param name="http">HTTP-клиент.</param>
    /// <param name="baseUrl">Базовый URL Elma365.</param>
    /// <param name="authToken">Bearer-токен.</param>
    /// <param name="logger">Логгер.</param>
    /// <exception cref="ArgumentNullException">Обязательный аргумент равен <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">URL или токен пуст, либо URL не является абсолютным HTTP(S)-адресом.</exception>
    public Elma365Client(
        HttpClient http,
        string baseUrl,
        string authToken,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authToken);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"BaseUrl Elma365 должен быть абсолютным HTTP(S)-адресом, получено: '{baseUrl}'.",
                nameof(baseUrl));
        }

        _httpClient = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _authToken = NormalizeToken(authToken);
        _logger = logger;

        if (_authToken.Length == 0)
        {
            throw new ArgumentException("Токен Elma365 пуст.", nameof(authToken));
        }
    }

    /// <summary>
    /// Получает элемент приложения Elma365 по идентификатору.
    /// </summary>
    /// <param name="appNamespace">Пространство имён приложения.</param>
    /// <param name="appCode">Код приложения.</param>
    /// <param name="objectId">Идентификатор объекта.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>JSON-объект элемента.</returns>
    public async Task<JObject> GetObjectAsync(
        string appNamespace,
        string appCode,
        string objectId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(appCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        var path =
            $"{ApiPrefix}/{Escape(appNamespace)}/{Escape(appCode)}/{Escape(objectId)}/get";
        var body = await GetStringAsync(path, ct).ConfigureAwait(false);
        return ParseObject(body, path);
    }

    /// <summary>
    /// Ищет объект приложения через POST /list с телом-фильтром.
    /// Если сервер вернул несколько объектов, используется первый.
    /// </summary>
    /// <param name="appNamespace">Пространство имён приложения.</param>
    /// <param name="appCode">Код приложения.</param>
    /// <param name="filterBody">JSON-тело фильтра.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Первый найденный JSON-объект.</returns>
    public async Task<JObject> FindObjectAsync(
        string appNamespace,
        string appCode,
        string filterBody,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(appCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterBody);

        var path = $"{ApiPrefix}/{Escape(appNamespace)}/{Escape(appCode)}/list";
        var body = await PostJsonAsync(path, filterBody, ct).ConfigureAwait(false);
        return ExtractSingleObject(body, path);
    }

    /// <summary>
    /// Получает список типов объектов приложения.
    /// </summary>
    /// <param name="appNamespace">Пространство имён приложения.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>JSON-массив типов объектов.</returns>
    public async Task<JArray> GetObjectTypesAsync(string appNamespace, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appNamespace);

        // DEFAULT: контракт Test Builder; при необходимости замените на endpoint вашей инсталляции.
        var path = $"{ApiPrefix}/{Escape(appNamespace)}/objects";
        var body = await GetStringAsync(path, ct).ConfigureAwait(false);
        return ParseArray(body, path);
    }

    /// <summary>
    /// Получает схему полей приложения.
    /// </summary>
    /// <param name="appNamespace">Пространство имён приложения.</param>
    /// <param name="appCode">Код приложения.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>JSON-объект схемы.</returns>
    public async Task<JObject> GetObjectSchemaAsync(
        string appNamespace,
        string appCode,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(appCode);

        // DEFAULT: контракт Test Builder; при необходимости замените на endpoint вашей инсталляции.
        var path =
            $"{ApiPrefix}/{Escape(appNamespace)}/{Escape(appCode)}/schema";
        var body = await GetStringAsync(path, ct).ConfigureAwait(false);
        return ParseObject(body, path);
    }

    private static string NormalizeToken(string token)
    {
        var normalized = token
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        if (normalized.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Authorization:".Length..].Trim();
        }

        if (normalized.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Bearer ".Length..].Trim();
        }

        if (normalized.Length >= 2 &&
            ((normalized[0] == '"' && normalized[^1] == '"') ||
             (normalized[0] == '\'' && normalized[^1] == '\'')))
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }

    private async Task<string> GetStringAsync(string path, CancellationToken ct)
    {
        var url = _baseUrl + path;

        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _authToken);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new Elma365Exception($"Сетевая ошибка при обращении к Elma365: {ex.Message}", ex);
            }

            using (response)
            {
                var body = await response.Content
                    .ReadAsStringAsync(ct)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return body;
                }

                var statusCode = (int)response.StatusCode;
                if (IsRetryable(statusCode) && attempt < RetryDelays.Length)
                {
                    var delay = RetryDelays[attempt];
                    _logger.LogWarning(
                        "Elma365 вернул HTTP {StatusCode} для {Path}. Повтор через {Delay} мс ({Attempt}/{MaxRetries}).",
                        statusCode,
                        path,
                        delay.TotalMilliseconds,
                        attempt + 1,
                        RetryDelays.Length);

                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                ThrowForStatus(statusCode, path, body);
            }
        }

        // Недостижимо: ThrowForStatus всегда бросает исключение для не-success ответа.
        throw new Elma365Exception($"Не удалось получить ответ Elma365 для '{path}'.");
    }

    private async Task<string> PostJsonAsync(string path, string jsonBody, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        using var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _authToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new Elma365Exception($"Сетевая ошибка при обращении к Elma365: {ex.Message}", ex);
        }

        using (response)
        {
            var body = await response.Content
                .ReadAsStringAsync(ct)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            ThrowForStatus((int)response.StatusCode, path, body);
        }

        throw new Elma365Exception($"Не удалось получить ответ Elma365 для '{path}'.");
    }

    private JObject ExtractSingleObject(string body, string path)
    {
        JToken token;
        try
        {
            token = JToken.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new Elma365Exception(
                $"Elma365 вернул некорректный JSON для '{path}': {ex.Message}",
                ex);
        }

        if (token is JArray array)
        {
            return TakeFirstObject(array, path);
        }

        if (token is JObject jsonObject)
        {
            foreach (var propertyName in new[] { "items", "result", "data" })
            {
                if (jsonObject.Property(propertyName, StringComparison.OrdinalIgnoreCase)?.Value is JArray items)
                {
                    return TakeFirstObject(items, path);
                }
            }

            return jsonObject;
        }

        throw new Elma365Exception(
            $"Elma365 вернул JSON, не являющийся объектом или массивом объектов, для '{path}'.");
    }

    private JObject TakeFirstObject(JArray array, string path)
    {
        if (array.Count == 0)
        {
            throw new Elma365NotFoundException(
                $"Elma365 не вернул объектов по фильтру для '{path}'.",
                200,
                array.ToString(Formatting.None));
        }

        if (array.Count > 1)
        {
            _logger.LogWarning(
                "Elma365 вернул {Count} объектов по фильтру для {Path}; используется первый.",
                array.Count,
                path);
        }

        return array[0] as JObject
               ?? throw new Elma365Exception(
                   $"Первый элемент ответа Elma365 для '{path}' не является JSON-объектом.");
    }

    private static bool IsRetryable(int statusCode)
    {
        return statusCode is (int)HttpStatusCode.TooManyRequests or (int)HttpStatusCode.ServiceUnavailable;
    }

    private static void ThrowForStatus(int statusCode, string path, string body)
    {
        var message = $"Elma365 вернул HTTP {statusCode} для '{path}'.";
        var responseBody = Truncate(body, 2000);

        switch (statusCode)
        {
            case (int)HttpStatusCode.Unauthorized:
            case (int)HttpStatusCode.Forbidden:
                throw new Elma365AuthException(message, statusCode, responseBody);

            case (int)HttpStatusCode.NotFound:
                throw new Elma365NotFoundException(message, statusCode, responseBody);

            case (int)HttpStatusCode.TooManyRequests:
                throw new Elma365ServerException(message, statusCode, responseBody);

            default:
                if (statusCode >= 500)
                {
                    throw new Elma365ServerException(message, statusCode, responseBody);
                }

                throw new Elma365Exception(message, statusCode, responseBody);
        }
    }

    private static JObject ParseObject(string body, string path)
    {
        try
        {
            return JObject.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new Elma365Exception(
                $"Elma365 вернул некорректный JSON-объект для '{path}': {ex.Message}",
                ex);
        }
    }

    private static JArray ParseArray(string body, string path)
    {
        try
        {
            var token = JToken.Parse(body);
            if (token is JArray array)
            {
                return array;
            }

            if (token is JObject jsonObject &&
                jsonObject.Property("items", StringComparison.OrdinalIgnoreCase)?.Value is JArray items)
            {
                return items;
            }

            throw new Elma365Exception(
                $"Elma365 вернул JSON, не являющийся массивом или объектом с полем 'items', для '{path}'.");
        }
        catch (JsonException ex)
        {
            throw new Elma365Exception(
                $"Elma365 вернул некорректный JSON для '{path}': {ex.Message}",
                ex);
        }
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}
