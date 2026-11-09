using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;

namespace TestBuilder.Blocks.Api;

/// <summary>
/// Поддерживаемый HTTP-метод.
/// </summary>
public enum HttpMethodType
{
    /// <summary>GET.</summary>
    [Description("GET")]
    Get,

    /// <summary>POST.</summary>
    [Description("POST")]
    Post,

    /// <summary>PUT.</summary>
    [Description("PUT")]
    Put,

    /// <summary>DELETE.</summary>
    [Description("DELETE")]
    Delete,
}

/// <summary>
/// Тип тела HTTP-запроса.
/// </summary>
public enum HttpBodyType
{
    /// <summary>Тело отсутствует.</summary>
    None,

    /// <summary>Тело в формате JSON.</summary>
    Json,

    /// <summary>Тело в формате application/x-www-form-urlencoded.</summary>
    FormUrlEncoded,

    /// <summary>Сырое текстовое тело.</summary>
    Raw,
}

/// <summary>
/// Тип авторизации HTTP-запроса.
/// </summary>
public enum HttpAuthType
{
    /// <summary>Без авторизации.</summary>
    None,

    /// <summary>HTTP Basic.</summary>
    Basic,

    /// <summary>Bearer-токен.</summary>
    Bearer,

    /// <summary>API-ключ в произвольном заголовке.</summary>
    ApiKeyHeader,
}

/// <summary>
/// Блок отправки HTTP-запроса с проверками ответа.
/// </summary>
public sealed class ApiRequestBlock : ITestBlock, IValidatableBlock
{
    /// <summary>
    /// Стабильный строковый тип блока.
    /// </summary>
    public const string TypeName = "apiRequest";

    private const int BodyLogLimit = 8192;

    private static readonly Regex VariableNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    [BlockProperty("Название", 0)]
    public string Title { get; set; } = "Отправка API запроса";

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public string BlockType => TypeName;

    /// <summary>
    /// HTTP-метод запроса.
    /// </summary>
    [BlockProperty("Метод", 1)]
    public HttpMethodType Method { get; set; } = HttpMethodType.Get;

    /// <summary>
    /// URL запроса. Поддерживаются шаблоны и относительные URL при заданном BaseAddress.
    /// </summary>
    [BlockProperty("URL", 2, PropertyEditorType.MultilineText, "Например: {{baseUrl}}/api/items")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// HTTP-заголовки запроса.
    /// </summary>
    [BlockProperty("Заголовки", 3, PropertyEditorType.KeyValueTable)]
    public List<KeyValueItem> Headers { get; set; } = new();

    /// <summary>
    /// Query-параметры запроса.
    /// </summary>
    [BlockProperty("Query-параметры", 4, PropertyEditorType.KeyValueTable)]
    public List<KeyValueItem> QueryParams { get; set; } = new();

    /// <summary>
    /// Тип тела запроса.
    /// </summary>
    [BlockProperty("Тип тела", 5)]
    public HttpBodyType BodyType { get; set; } = HttpBodyType.None;

    /// <summary>
    /// Тело запроса. Для FormUrlEncoded задаётся в формате <c>key=value&amp;key2=value2</c>.
    /// Поддерживаются генераторы <c>{{random.uuid}}</c>, <c>{{random.number}}</c>
    /// и <c>{{random.number:min:max}}</c>.
    /// </summary>
    [BlockProperty("Тело", 6, PropertyEditorType.MultilineText)]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Сохранять JSON-тело запроса в переменную контекста.
    /// </summary>
    [BlockProperty(
        "Сохранить json запроса",
        7,
        PropertyEditorType.Auto,
        "Доступно для типа тела JSON. Значение сохраняется до отправки запроса.")]
    public bool SaveRequestJson { get; set; }

    /// <summary>
    /// Имя переменной для сохранения JSON-тела запроса.
    /// </summary>
    [BlockProperty(
        "Переменная JSON запроса",
        8,
        PropertyEditorType.Auto,
        "Только латиница, цифры и подчёркивание; например requestJson.")]
    public string SaveRequestJsonVariable { get; set; } = string.Empty;

    /// <summary>
    /// Тип авторизации.
    /// </summary>
    [BlockProperty(
        "Авторизация",
        9,
        PropertyEditorType.Auto,
        "Basic/Bearer автоматически добавляют заголовок Authorization.")]
    public HttpAuthType AuthType { get; set; } = HttpAuthType.None;

    /// <summary>
    /// Пользователь Basic-авторизации.
    /// </summary>
    [BlockProperty("Basic: пользователь", 10, PropertyEditorType.Secret)]
    public string AuthUser { get; set; } = string.Empty;

    /// <summary>
    /// Пароль Basic-авторизации.
    /// </summary>
    [BlockProperty("Basic: пароль", 11, PropertyEditorType.Secret)]
    public string AuthPassword { get; set; } = string.Empty;

    /// <summary>
    /// Bearer-токен.
    /// </summary>
    [BlockProperty(
        "Bearer-токен",
        12,
        PropertyEditorType.Secret,
        "Заголовок Authorization формируется автоматически.")]
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Имя заголовка для API-ключа.
    /// </summary>
    [BlockProperty("API-key: заголовок", 13, PropertyEditorType.Secret)]
    public string AuthApiKeyName { get; set; } = string.Empty;

    /// <summary>
    /// Значение API-ключа.
    /// </summary>
    [BlockProperty("API-key: значение", 14, PropertyEditorType.Secret)]
    public string AuthApiKeyValue { get; set; } = string.Empty;

    /// <summary>
    /// Таймаут запроса в секундах.
    /// </summary>
    [BlockProperty("Таймаут, с", 15)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Проверки ответа. Если список пуст, проверяется только HTTP-статус 2xx.
    /// </summary>
    [BlockProperty("Проверки ответа", 16, PropertyEditorType.JsonPathCheckTable)]
    public List<JsonPathCheck> ResponseChecks { get; set; } = new();

    /// <summary>
    /// Имя переменной, в которую сохраняется разобранный JSON ответа.
    /// </summary>
    [BlockProperty("Сохранить ответ в переменную", 17)]
    public string SaveResponseVariable { get; set; } = string.Empty;

    /// <inheritdoc />
    public async Task<BlockResult> ExecuteAsync(ExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (TimeoutSeconds <= 0)
        {
            return CreateResult(
                success: false,
                message: "Таймаут запроса должен быть больше нуля.",
                elapsed: TimeSpan.Zero,
                output: new Dictionary<string, object>());
        }

        if (SaveRequestJson)
        {
            if (BodyType != HttpBodyType.Json)
            {
                return CreateResult(
                    success: false,
                    message: "Сохранить JSON запроса можно только при типе тела Json.",
                    elapsed: TimeSpan.Zero,
                    output: new Dictionary<string, object>());
            }

            if (!VariableNameRegex.IsMatch(SaveRequestJsonVariable?.Trim() ?? string.Empty))
            {
                return CreateResult(
                    success: false,
                    message: "Имя переменной JSON запроса должно содержать только латиницу, цифры и подчёркивание.",
                    elapsed: TimeSpan.Zero,
                    output: new Dictionary<string, object>());
            }
        }

        var stopwatch = Stopwatch.StartNew();
        HttpRequestMessage? request = null;

        try
        {
            request = BuildRequest(ctx);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                ctx.CancellationToken,
                timeoutCts.Token);

            using var response = await ctx.HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
                .ConfigureAwait(false);

            var bodyText = await response.Content
                .ReadAsStringAsync(linkedCts.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();
            return EvaluateResponse(ctx, response, bodyText, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (!ctx.CancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            ctx.Logger.LogWarning(
                "ApiRequestBlock: запрос не уложился в таймаут {TimeoutSeconds} с.",
                TimeoutSeconds);

            return CreateResult(
                success: false,
                message: $"HTTP-запрос не уложился в таймаут {TimeoutSeconds} с.",
                elapsed: stopwatch.Elapsed,
                output: new Dictionary<string, object> { ["timeoutSeconds"] = TimeoutSeconds });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            ctx.Logger.LogWarning(ex, "ApiRequestBlock: сетевая ошибка при выполнении запроса.");
            return CreateResult(
                success: false,
                message: $"Сетевая ошибка HTTP-запроса: {ex.Message}",
                elapsed: stopwatch.Elapsed,
                output: new Dictionary<string, object> { ["error"] = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or UriFormatException or FormatException)
        {
            stopwatch.Stop();
            ctx.Logger.LogWarning(ex, "ApiRequestBlock: некорректная конфигурация запроса.");
            return CreateResult(
                success: false,
                message: $"Некорректный HTTP-запрос: {ex.Message}",
                elapsed: stopwatch.Elapsed,
                output: new Dictionary<string, object> { ["error"] = ex.Message });
        }
        finally
        {
            request?.Dispose();
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            yield return "URL обязателен.";
        }

        if (TimeoutSeconds <= 0)
        {
            yield return "Таймаут запроса должен быть больше нуля.";
        }

        if (SaveRequestJson)
        {
            if (BodyType != HttpBodyType.Json)
            {
                yield return "Сохранить JSON запроса можно только при типе тела Json.";
            }

            if (string.IsNullOrWhiteSpace(SaveRequestJsonVariable))
            {
                yield return "Укажите имя переменной для сохранения JSON запроса.";
            }
            else if (!VariableNameRegex.IsMatch(SaveRequestJsonVariable))
            {
                yield return "Имя переменной JSON запроса должно содержать только латиницу, цифры и подчёркивание и начинаться с буквы или подчёркивания.";
            }
        }

        switch (AuthType)
        {
            case HttpAuthType.Basic when string.IsNullOrWhiteSpace(AuthUser):
                yield return "Для Basic-авторизации укажите пользователя.";
                break;

            case HttpAuthType.Bearer when string.IsNullOrWhiteSpace(AuthToken):
                yield return "Для Bearer-авторизации укажите токен.";
                break;

            case HttpAuthType.ApiKeyHeader when string.IsNullOrWhiteSpace(AuthApiKeyName):
                yield return "Для авторизации по API-ключу укажите имя заголовка.";
                break;

            case HttpAuthType.ApiKeyHeader when string.IsNullOrWhiteSpace(AuthApiKeyValue):
                yield return "Для авторизации по API-ключу укажите значение.";
                break;
        }
    }

    private HttpRequestMessage BuildRequest(ExecutionContext ctx)
    {
        var url = Resolve(ctx, Url).Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("URL не задан.");
        }

        var resolvedQuery = ResolveKeyValues(ctx, QueryParams);
        var urlWithQuery = AppendQuery(url, resolvedQuery);

        var method = Method switch
        {
            HttpMethodType.Get => HttpMethod.Get,
            HttpMethodType.Post => HttpMethod.Post,
            HttpMethodType.Put => HttpMethod.Put,
            HttpMethodType.Delete => HttpMethod.Delete,
            _ => throw new InvalidOperationException($"Неподдерживаемый HTTP-метод: {Method}."),
        };

        var request = new HttpRequestMessage(
            method,
            new Uri(urlWithQuery, UriKind.RelativeOrAbsolute));

        try
        {
            var bodyText = BodyType == HttpBodyType.Json
                ? ctx.Templates.ResolveJson(Body, ctx.Variables)
                : Resolve(ctx, Body);

            request.Content = BuildContent(bodyText);

            if (BodyType == HttpBodyType.Json && SaveRequestJson)
            {
                StoreRequestJson(ctx, bodyText);
            }

            ApplyHeaders(ctx, request);
            ApplyAuth(ctx, request);
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    private void StoreRequestJson(ExecutionContext ctx, string bodyText)
    {
        var variableName = SaveRequestJsonVariable.Trim();

        try
        {
            ctx.Variables[variableName] = JToken.Parse(bodyText);
            ctx.Logger.LogDebug(
                "ApiRequestBlock: JSON запроса сохранён в переменную '{Variable}'.",
                variableName);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Не удалось сохранить JSON запроса в '{variableName}': тело не является корректным JSON.",
                ex);
        }
    }

    private HttpContent? BuildContent(string bodyText)
    {
        return BodyType switch
        {
            HttpBodyType.None => null,
            HttpBodyType.Json => new StringContent(
                bodyText,
                Encoding.UTF8,
                "application/json"),
            HttpBodyType.FormUrlEncoded => new FormUrlEncodedContent(
                ParseFormBody(bodyText)),
            HttpBodyType.Raw => new StringContent(bodyText, Encoding.UTF8),
            _ => throw new InvalidOperationException($"Неподдерживаемый тип тела: {BodyType}."),
        };
    }

    private void ApplyHeaders(ExecutionContext ctx, HttpRequestMessage request)
    {
        foreach (var header in ResolveKeyValues(ctx, Headers))
        {
            var added = request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (!added && request.Content is not null)
            {
                added = request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (!added)
            {
                throw new InvalidOperationException($"Не удалось добавить HTTP-заголовок '{header.Key}'.");
            }
        }
    }

    private void ApplyAuth(ExecutionContext ctx, HttpRequestMessage request)
    {
        switch (AuthType)
        {
            case HttpAuthType.None:
                return;

            case HttpAuthType.Basic:
            {
                var user = Resolve(ctx, AuthUser);
                if (string.IsNullOrEmpty(user))
                {
                    throw new InvalidOperationException("Для Basic-авторизации не задан пользователь.");
                }

                var password = Resolve(ctx, AuthPassword);
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                return;
            }

            case HttpAuthType.Bearer:
            {
                var token = NormalizeBearerToken(Resolve(ctx, AuthToken));
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("Для Bearer-авторизации не задан токен.");
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return;
            }

            case HttpAuthType.ApiKeyHeader:
            {
                var headerName = Resolve(ctx, AuthApiKeyName).Trim();
                var headerValue = Resolve(ctx, AuthApiKeyValue);
                if (string.IsNullOrWhiteSpace(headerName))
                {
                    throw new InvalidOperationException("Для API-key-авторизации не задано имя заголовка.");
                }

                SetHeader(request, headerName, headerValue);
                return;
            }

            default:
                throw new InvalidOperationException($"Неподдерживаемый тип авторизации: {AuthType}.");
        }
    }

    private BlockResult EvaluateResponse(
        ExecutionContext ctx,
        HttpResponseMessage response,
        string bodyText,
        TimeSpan elapsed)
    {
        var statusCode = (int)response.StatusCode;
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? response.StatusCode.ToString()
            : response.ReasonPhrase;

        var output = new Dictionary<string, object>
        {
            ["status"] = statusCode,
            ["headers"] = CollectHeaders(response),
            ["body"] = Truncate(bodyText, BodyLogLimit),
            ["bodyLength"] = bodyText.Length,
        };

        var jsonBody = TryParseJson(bodyText);
        var saveVariable = Resolve(ctx, SaveResponseVariable).Trim();
        if (!string.IsNullOrWhiteSpace(saveVariable))
        {
            ctx.Variables[saveVariable] = jsonBody ?? new JValue(bodyText);
        }

        var checks = ResponseChecks ?? new List<JsonPathCheck>();
        var failures = new List<string>();

        if (checks.Count == 0)
        {
            if (!response.IsSuccessStatusCode)
            {
                failures.Add($"HTTP {statusCode} {reason} не является успешным статусом.");
            }
        }
        else
        {
            foreach (var check in checks)
            {
                if (check is null)
                {
                    failures.Add("В списке проверок найден пустой элемент.");
                    continue;
                }

                var path = Resolve(ctx, check.Path).Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = "$";
                }

                var resolvedCheck = new JsonPathCheck
                {
                    Path = path,
                    Operator = check.Operator,
                    ExpectedValue = Resolve(ctx, check.ExpectedValue),
                };

                JToken? token;
                if (IsStatusPath(path))
                {
                    token = new JValue(statusCode);
                }
                else if (jsonBody is null)
                {
                    failures.Add(
                        $"Проверка '{path}' ({check.Operator}) не выполнена: тело ответа не является JSON.");
                    continue;
                }
                else
                {
                    try
                    {
                        token = jsonBody.SelectToken(path);
                    }
                    catch (JsonException ex)
                    {
                        failures.Add($"Некорректный JSONPath '{path}': {ex.Message}");
                        continue;
                    }
                }

                var (success, message) = CheckEvaluator.Evaluate(token, resolvedCheck);
                if (!success)
                {
                    failures.Add(message);
                }
            }
        }

        if (failures.Count > 0)
        {
            var message = $"HTTP {statusCode} {reason}. Проверки не пройдены:" +
                          System.Environment.NewLine +
                          string.Join(
                              System.Environment.NewLine,
                              failures.Select(failure => " • " + failure));

            ctx.Logger.LogWarning(
                "ApiRequestBlock: {Method} {Url} -> HTTP {Status}, ошибок проверок: {Count}.",
                Method,
                Url,
                statusCode,
                failures.Count);

            return CreateResult(false, message, elapsed, output);
        }

        ctx.Logger.LogInformation(
            "ApiRequestBlock: {Method} {Url} -> HTTP {Status} за {Duration}.",
            Method,
            Url,
            statusCode,
            elapsed);

        return CreateResult(
            true,
            $"HTTP {statusCode} {reason}. Проверки пройдены.",
            elapsed,
            output);
    }

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in response.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        return headers;
    }

    private static bool IsStatusPath(string path)
    {
        return string.Equals(path, "$status", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "status", StringComparison.OrdinalIgnoreCase);
    }

    private static JToken? TryParseJson(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JToken.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseFormBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            yield break;
        }

        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=');
            var key = separatorIndex >= 0 ? part[..separatorIndex] : part;
            var value = separatorIndex >= 0 ? part[(separatorIndex + 1)..] : string.Empty;

            yield return new KeyValuePair<string, string>(
                WebUtility.UrlDecode(key),
                WebUtility.UrlDecode(value));
        }
    }

    private static string AppendQuery(string url, IReadOnlyCollection<KeyValueItem> queryParams)
    {
        if (queryParams.Count == 0)
        {
            return url;
        }

        var fragmentIndex = url.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
        var baseUrl = fragmentIndex >= 0 ? url[..fragmentIndex] : url;

        var separator = baseUrl.Contains('?')
            ? (baseUrl.EndsWith('?') || baseUrl.EndsWith('&') ? string.Empty : "&")
            : "?";

        var query = string.Join(
            "&",
            queryParams.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));

        return baseUrl + separator + query + fragment;
    }

    private List<KeyValueItem> ResolveKeyValues(
        ExecutionContext ctx,
        IEnumerable<KeyValueItem>? source)
    {
        var result = new List<KeyValueItem>();
        if (source is null)
        {
            return result;
        }

        foreach (var item in source)
        {
            if (item is null)
            {
                continue;
            }

            var key = Resolve(ctx, item.Key).Trim();
            if (key.Length == 0)
            {
                continue;
            }

            result.Add(new KeyValueItem
            {
                Key = key,
                Value = Resolve(ctx, item.Value),
            });
        }

        return result;
    }

    private static string Resolve(ExecutionContext ctx, string? value)
    {
        return ctx.Templates.Resolve(value, ctx.Variables);
    }

    private static string NormalizeBearerToken(string rawToken)
    {
        var token = rawToken
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        if (token.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Authorization:".Length..].Trim();
        }

        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }

        if (token.Length >= 2 &&
            ((token[0] == '"' && token[^1] == '"') ||
             (token[0] == '\'' && token[^1] == '\'')))
        {
            token = token[1..^1].Trim();
        }

        return token;
    }

    private static void SetHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Content?.Headers.Remove(name);

        if (request.Headers.TryAddWithoutValidation(name, value))
        {
            return;
        }

        if (request.Content is not null &&
            request.Content.Headers.TryAddWithoutValidation(name, value))
        {
            return;
        }

        throw new InvalidOperationException($"Не удалось установить HTTP-заголовок '{name}'.");
    }

    private static BlockResult CreateResult(
        bool success,
        string message,
        TimeSpan elapsed,
        Dictionary<string, object> output)
    {
        output["durationMs"] = elapsed.TotalMilliseconds;

        return new BlockResult
        {
            Success = success,
            Message = message,
            Duration = elapsed,
            Output = output,
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] +
               $"… [обрезано, всего {value.Length.ToString(CultureInfo.InvariantCulture)} символов]";
    }
}
