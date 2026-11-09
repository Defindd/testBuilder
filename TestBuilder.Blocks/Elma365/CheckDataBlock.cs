using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;
using TestBuilder.Elma365;

namespace TestBuilder.Blocks.Elma365;

/// <summary>
/// Способ поиска объекта в Elma365.
/// </summary>
public enum CheckDataSearchMode
{
    /// <summary>Получение по ID: GET /{namespace}/{code}/{id}/get.</summary>
    [Description("По ID")]
    ByObjectId,

    /// <summary>Поиск по фильтру: POST /{namespace}/{code}/list.</summary>
    [Description("По фильтру")]
    ByFilter,
}

/// <summary>
/// Блок проверки элемента приложения Elma365.
/// </summary>
public sealed class CheckDataBlock : ITestBlock, IValidatableBlock
{
    /// <summary>
    /// Стабильный строковый тип блока.
    /// </summary>
    public const string TypeName = "checkData";

    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    [BlockProperty("Название", 0)]
    public string Title { get; set; } = "Проверить данные (Elma365)";

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public string BlockType => TypeName;

    /// <summary>
    /// Базовый URL Elma365. Можно задать шаблон <c>{{baseUrl}}</c>.
    /// Если поле пустое, используется переменная контекста <c>baseUrl</c>.
    /// </summary>
    [BlockProperty("Base URL", 1, PropertyEditorType.MultilineText, "Например: {{baseUrl}}")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Имя переменной контекста с Bearer-токеном.
    /// </summary>
    [BlockProperty("Переменная с токеном", 2)]
    public string AuthTokenVariable { get; set; } = "elmaToken";

    /// <summary>
    /// Пространство имён приложения.
    /// </summary>
    [BlockProperty("Пространство имён", 3)]
    public string AppNamespace { get; set; } = string.Empty;

    /// <summary>
    /// Код приложения.
    /// </summary>
    [BlockProperty("Код приложения", 4)]
    public string AppCode { get; set; } = string.Empty;

    /// <summary>
    /// Способ поиска: по ID или по фильтру.
    /// </summary>
    [BlockProperty("Поиск", 5)]
    public CheckDataSearchMode SearchMode { get; set; } = CheckDataSearchMode.ByObjectId;

    /// <summary>
    /// Идентификатор объекта. Используется в режиме поиска по ID.
    /// </summary>
    [BlockProperty("ID объекта", 6, PropertyEditorType.Auto, "Например: {{createdObjectId}}")]
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// JSON-тело фильтра. Используется в режиме поиска по фильтру.
    /// </summary>
    [BlockProperty(
        "Фильтр",
        7,
        PropertyEditorType.MultilineText,
        "JSON-тело для POST /pub/v1/app/{namespace}/{code}/list")]
    public string Filter { get; set; } = string.Empty;

    /// <summary>
    /// Проверки полей объекта.
    /// </summary>
    [BlockProperty("Проверки полей", 8, PropertyEditorType.JsonPathCheckTable)]
    public List<JsonPathCheck> FieldChecks { get; set; } = new();

    /// <summary>
    /// Имя переменной, в которую сохраняется полученный объект.
    /// </summary>
    [BlockProperty("Сохранить объект в переменную", 9)]
    public string SaveObjectVariable { get; set; } = string.Empty;

    /// <summary>
    /// Совместимость со старыми файлами, где поле называлось <c>objectType</c>.
    /// При чтении переносится в <see cref="AppCode"/>, при записи не сериализуется.
    /// </summary>
    [JsonPropertyName("objectType")]
    [System.Text.Json.Serialization.JsonIgnore(
        Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyObjectType
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(AppCode))
            {
                AppCode = value;
            }
        }
    }

    /// <inheritdoc />
    public async Task<BlockResult> ExecuteAsync(ExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var baseUrl = Resolve(ctx, BaseUrl).Trim();
            if (baseUrl.Length == 0 &&
                ctx.Variables.TryGetValue("baseUrl", out var baseUrlVariable))
            {
                baseUrl = baseUrlVariable?.ToString()?.Trim() ?? string.Empty;
            }

            if (baseUrl.Length == 0)
            {
                return Fail("Не задан BaseUrl Elma365 (свойство блока или переменная baseUrl).", stopwatch.Elapsed);
            }

            var appNamespace = Resolve(ctx, AppNamespace).Trim();
            var appCode = Resolve(ctx, AppCode).Trim();
            var objectId = Resolve(ctx, ObjectId).Trim();
            var filter = ctx.Templates.ResolveJson(Filter, ctx.Variables).Trim();

            if (appNamespace.Length == 0)
            {
                return Fail("Не задано пространство имён приложения Elma365.", stopwatch.Elapsed);
            }

            if (appCode.Length == 0)
            {
                return Fail("Не задан код приложения Elma365.", stopwatch.Elapsed);
            }

            if (SearchMode == CheckDataSearchMode.ByObjectId && objectId.Length == 0)
            {
                return Fail("Не задан идентификатор объекта Elma365.", stopwatch.Elapsed);
            }

            if (SearchMode == CheckDataSearchMode.ByFilter && filter.Length == 0)
            {
                return Fail("Не задан фильтр для поиска объекта Elma365.", stopwatch.Elapsed);
            }

            var tokenVariable = Resolve(ctx, AuthTokenVariable).Trim();
            if (tokenVariable.Length == 0)
            {
                tokenVariable = "elmaToken";
            }

            if (!ctx.Variables.TryGetValue(tokenVariable, out var tokenValue) ||
                string.IsNullOrWhiteSpace(tokenValue?.ToString()))
            {
                return Fail(
                    $"Переменная '{tokenVariable}' с токеном Elma365 не найдена или пуста.",
                    stopwatch.Elapsed);
            }

            var client = new Elma365Client(
                ctx.HttpClient,
                baseUrl,
                tokenValue!.ToString()!,
                ctx.Logger);

            var obj = SearchMode switch
            {
                CheckDataSearchMode.ByObjectId => await client
                    .GetObjectAsync(appNamespace, appCode, objectId, ctx.CancellationToken)
                    .ConfigureAwait(false),
                CheckDataSearchMode.ByFilter => await client
                    .FindObjectAsync(appNamespace, appCode, filter, ctx.CancellationToken)
                    .ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Неизвестный режим поиска Elma365: {SearchMode}."),
            };

            stopwatch.Stop();
            var checks = FieldChecks ?? new List<JsonPathCheck>();
            var failures = new List<string>();

            foreach (var check in checks)
            {
                if (check is null)
                {
                    failures.Add("В списке проверок найден пустой элемент.");
                    continue;
                }

                var path = Resolve(ctx, check.Path).Trim();
                if (path.Length == 0)
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
                try
                {
                    token = obj.SelectToken(path);
                }
                catch (JsonException ex)
                {
                    failures.Add($"Некорректный JSONPath '{path}': {ex.Message}");
                    continue;
                }

                var (success, message) = CheckEvaluator.Evaluate(token, resolvedCheck);
                if (!success)
                {
                    failures.Add(message);
                }
            }

            var output = new Dictionary<string, object>
            {
                ["namespace"] = appNamespace,
                ["appCode"] = appCode,
                ["searchMode"] = SearchMode.ToString(),
                ["objectId"] = objectId,
                ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds,
            };

            if (failures.Count > 0)
            {
                var message =
                    "Объект Elma365 получен, но проверки полей не пройдены:" +
                    System.Environment.NewLine +
                    string.Join(
                        System.Environment.NewLine,
                        failures.Select(failure => " • " + failure));

                return new BlockResult
                {
                    Success = false,
                    Message = message,
                    Duration = stopwatch.Elapsed,
                    Output = output,
                };
            }

            var saveVariable = Resolve(ctx, SaveObjectVariable).Trim();
            if (saveVariable.Length > 0)
            {
                ctx.Variables[saveVariable] = obj;
            }

            ctx.Logger.LogInformation(
                "CheckDataBlock: {Namespace}/{AppCode} ({SearchMode}) прочитан за {Duration}.",
                appNamespace,
                appCode,
                SearchMode,
                stopwatch.Elapsed);

            return new BlockResult
            {
                Success = true,
                Message = $"Объект Elma365 {appCode} прочитан ({SearchMode}). Проверки пройдены.",
                Duration = stopwatch.Elapsed,
                Output = output,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Elma365AuthException ex)
        {
            stopwatch.Stop();
            return Fail($"Ошибка авторизации в Elma365: {ex.Message}", stopwatch.Elapsed, ex);
        }
        catch (Elma365NotFoundException ex)
        {
            stopwatch.Stop();
            return Fail($"Объект Elma365 не найден: {ex.Message}", stopwatch.Elapsed, ex);
        }
        catch (Elma365ServerException ex)
        {
            stopwatch.Stop();
            return Fail($"Ошибка сервера Elma365: {ex.Message}", stopwatch.Elapsed, ex);
        }
        catch (Elma365Exception ex)
        {
            stopwatch.Stop();
            return Fail($"Ошибка Elma365: {ex.Message}", stopwatch.Elapsed, ex);
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(AppNamespace))
        {
            yield return "Пространство имён приложения обязательно.";
        }

        if (string.IsNullOrWhiteSpace(AppCode))
        {
            yield return "Код приложения обязателен.";
        }

        if (SearchMode == CheckDataSearchMode.ByObjectId &&
            string.IsNullOrWhiteSpace(ObjectId))
        {
            yield return "Идентификатор объекта обязателен для поиска по ID.";
        }

        if (SearchMode == CheckDataSearchMode.ByFilter &&
            string.IsNullOrWhiteSpace(Filter))
        {
            yield return "Фильтр обязателен для поиска по фильтру.";
        }

        if (string.IsNullOrWhiteSpace(AuthTokenVariable))
        {
            yield return "Не задано имя переменной с токеном Elma365.";
        }
    }

    private static string Resolve(ExecutionContext ctx, string? value)
    {
        return ctx.Templates.Resolve(value, ctx.Variables);
    }

    private static BlockResult Fail(
        string message,
        TimeSpan elapsed,
        Elma365Exception? exception = null)
    {
        var output = new Dictionary<string, object>
        {
            ["durationMs"] = elapsed.TotalMilliseconds,
        };

        if (exception is not null)
        {
            output["error"] = exception.Message;
            if (exception.StatusCode is not null)
            {
                output["statusCode"] = exception.StatusCode.Value;
            }
        }

        return new BlockResult
        {
            Success = false,
            Message = message,
            Duration = elapsed,
            Output = output,
        };
    }
}
