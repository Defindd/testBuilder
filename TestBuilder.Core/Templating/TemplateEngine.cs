using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestBuilder.Core.Templating;

/// <summary>
/// Подставляет значения переменных в шаблоны вида <c>{{variable}}</c> и <c>{{variable.path.to.field}}</c>.
/// Поддерживает встроенные генераторы <c>{{random.uuid}}</c>,
/// <c>{{random.number}}</c> и <c>{{random.number:min:max}}</c>.
/// </summary>
public sealed class TemplateEngine
{
    private const int MaxDeepDepth = 16;
    private const long DefaultRandomNumberMin = 1;
    private const long DefaultRandomNumberMax = 1_000_000;

    private static readonly Regex PlaceholderRegex = new(
        @"\{\{\s*([\w\.\-:]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex JsonFragmentRegex = new(
        @"\{\$\s*([\w\.\-]+)\s*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger<TemplateEngine> _logger;

    /// <summary>
    /// Создаёт движок шаблонов.
    /// </summary>
    /// <param name="logger">Логгер; при отсутствии используется <see cref="NullLogger{T}"/>.</param>
    public TemplateEngine(ILogger<TemplateEngine>? logger = null)
    {
        _logger = logger ?? NullLogger<TemplateEngine>.Instance;
    }

    /// <summary>
    /// Подставляет значения переменных в строку.
    /// Неизвестные переменные заменяются пустой строкой и логируются как предупреждение.
    /// </summary>
    /// <param name="template">Строка с шаблонами.</param>
    /// <param name="variables">Доступные переменные.</param>
    /// <returns>Строка с подставленными значениями.</returns>
    public string Resolve(string? template, IDictionary<string, object>? variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var expression = match.Groups[1].Value;
            if (TryGenerateRandomValue(expression, out var generated))
            {
                return generated;
            }

            var segments = expression.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                return string.Empty;
            }

            if (variables is null || !TryGetVariable(variables, segments[0], out var root))
            {
                _logger.LogWarning(
                    "Переменная шаблона '{Variable}' не найдена. Подставлена пустая строка.",
                    segments[0]);
                return string.Empty;
            }

            object? value = root;
            for (var index = 1; index < segments.Length && value is not null; index++)
            {
                value = GetMember(value, segments[index]);
            }

            if (value is null)
            {
                _logger.LogWarning(
                    "Путь '{Path}' не найден в переменной '{Variable}'. Подставлена пустая строка.",
                    expression,
                    segments[0]);
                return string.Empty;
            }

            return FormatValue(value);
        });
    }

    /// <summary>
    /// Подставляет переменные и JSON-фрагменты вида <c>{$variable.path}</c>.
    /// JSON-фрагменты вставляются как компактный JSON; строки сериализуются в кавычках.
    /// Предназначено для JSON-тела запроса и JSON-фильтра.
    /// </summary>
    /// <param name="template">JSON-шаблон.</param>
    /// <param name="variables">Доступные переменные.</param>
    /// <returns>JSON-строка с подставленными значениями.</returns>
    public string ResolveJson(string? template, IDictionary<string, object>? variables)
    {
        var resolved = Resolve(template, variables);
        return JsonFragmentRegex.Replace(resolved, match =>
        {
            var expression = match.Groups[1].Value;
            var segments = expression.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return "null";
            }

            if (variables is null || !TryGetVariable(variables, segments[0], out var value))
            {
                _logger.LogWarning(
                    "JSON-переменная '{Variable}' не найдена. Вставлен null.",
                    segments[0]);
                return "null";
            }

            for (var index = 1; index < segments.Length && value is not null; index++)
            {
                value = GetMember(value, segments[index]);
            }

            if (value is null)
            {
                _logger.LogWarning(
                    "JSON-путь '{Path}' не найден. Вставлен null.",
                    expression);
                return "null";
            }

            return FormatJsonValue(value);
        });
    }

    /// <summary>
    /// Рекурсивно обходит объект и подставляет шаблоны во все доступные для записи строковые свойства.
    /// Используется блоками перед выполнением, чтобы не дублировать код в каждом блоке.
    /// </summary>
    /// <param name="target">Объект для обработки.</param>
    /// <param name="variables">Доступные переменные.</param>
    public void ResolveDeep(object? target, IDictionary<string, object>? variables)
    {
        if (target is null || variables is null)
        {
            return;
        }

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        WalkObject(target, variables, visited, depth: 0);
    }

    private bool TryGenerateRandomValue(string expression, out string value)
    {
        if (string.Equals(expression, "random.uuid", StringComparison.OrdinalIgnoreCase))
        {
            value = Guid.NewGuid().ToString("D");
            return true;
        }

        if (string.Equals(expression, "random.number", StringComparison.OrdinalIgnoreCase))
        {
            value = GenerateRandomNumber(DefaultRandomNumberMin, DefaultRandomNumberMax);
            return true;
        }

        const string NumberPrefix = "random.number:";
        if (!expression.StartsWith(NumberPrefix, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        var rangeText = expression[NumberPrefix.Length..];
        var parts = rangeText.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 &&
            long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var min) &&
            long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var max) &&
            min <= max)
        {
            value = GenerateRandomNumber(min, max);
            return true;
        }

        _logger.LogWarning(
            "Некорректный диапазон генератора '{Expression}'. Ожидается random.number:min:max.",
            expression);
        value = string.Empty;
        return true;
    }

    private static string GenerateRandomNumber(long minimum, long maximum)
    {
        if (minimum == maximum)
        {
            return minimum.ToString(CultureInfo.InvariantCulture);
        }

        // NextInt64 принимает верхнюю границу exclusive. Для long.MaxValue
        // используем её как exclusive, чтобы избежать переполнения.
        var exclusiveMaximum = maximum == long.MaxValue ? maximum : maximum + 1;
        var number = Random.Shared.NextInt64(minimum, exclusiveMaximum);
        return number.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryGetVariable(
        IDictionary<string, object> variables,
        string name,
        out object? value)
    {
        if (variables.TryGetValue(name, out value))
        {
            return true;
        }

        foreach (var pair in variables)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static object? GetMember(object current, string member)
    {
        switch (current)
        {
            case JObject jsonObject:
                return jsonObject.Property(member, StringComparison.OrdinalIgnoreCase)?.Value;

            case JArray jsonArray when int.TryParse(member, NumberStyles.Integer, CultureInfo.InvariantCulture, out var arrayIndex):
                return arrayIndex >= 0 && arrayIndex < jsonArray.Count ? jsonArray[arrayIndex] : null;

            case JValue:
                return null;

            case JsonElement element:
                return GetJsonElementMember(element, member);

            case IDictionary<string, object> genericDictionary:
                if (genericDictionary.TryGetValue(member, out var genericValue))
                {
                    return genericValue;
                }

                foreach (var pair in genericDictionary)
                {
                    if (string.Equals(pair.Key, member, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }

                return null;

            case IDictionary dictionary:
                if (dictionary.Contains(member))
                {
                    return dictionary[member];
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key &&
                        string.Equals(key, member, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }

                return null;
        }

        var type = current.GetType();
        var property = type.GetProperty(
            member,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is not null &&
            property.GetIndexParameters().Length == 0 &&
            property.CanRead)
        {
            return property.GetValue(current);
        }

        var field = type.GetField(
            member,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return field?.GetValue(current);
    }

    private static object? GetJsonElementMember(JsonElement element, string member)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(member, out var directProperty))
            {
                return directProperty;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, member, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }

            return null;
        }

        if (element.ValueKind == JsonValueKind.Array &&
            int.TryParse(member, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
        {
            if (index < 0 || index >= element.GetArrayLength())
            {
                return null;
            }

            return element[index];
        }

        return null;
    }

    private static string FormatJsonValue(object value)
    {
        return value switch
        {
            JValue jsonValue => jsonValue.Type == JTokenType.String
                ? JsonConvert.SerializeObject(jsonValue.Value)
                : jsonValue.ToString(Formatting.None),
            JToken jsonToken => jsonToken.ToString(Formatting.None),
            JsonElement element => element.GetRawText(),
            string text => JsonConvert.SerializeObject(text),
            bool boolean => boolean ? "true" : "false",
            null => "null",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => JsonConvert.SerializeObject(value),
        };
    }

    private static string FormatValue(object value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            JValue jsonValue => Convert.ToString(jsonValue.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            JToken jsonToken => jsonToken.Type == JTokenType.String
                ? jsonToken.Value<string>() ?? string.Empty
                : jsonToken.ToString(Formatting.None),
            JsonElement element => FormatJsonElement(element),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string FormatJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText(),
        };
    }

    private void WalkObject(
        object target,
        IDictionary<string, object> variables,
        HashSet<object> visited,
        int depth)
    {
        if (depth > MaxDeepDepth || target is string || target.GetType().IsValueType)
        {
            return;
        }

        if (target is JToken || target is Type || target is Delegate)
        {
            return;
        }

        if (!visited.Add(target))
        {
            return;
        }

        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length != 0 || !property.CanRead)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(target);
            }
            catch (Exception ex) when (ex is TargetInvocationException or NotSupportedException)
            {
                _logger.LogDebug(ex, "Не удалось прочитать свойство '{Property}' при разрешении шаблонов.", property.Name);
                continue;
            }

            if (value is null)
            {
                continue;
            }

            if (property.PropertyType == typeof(string))
            {
                if (property.CanWrite)
                {
                    var resolved = Resolve((string)value, variables);
                    if (!string.Equals(resolved, (string)value, StringComparison.Ordinal))
                    {
                        property.SetValue(target, resolved);
                    }
                }

                continue;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is not null)
                    {
                        WalkObject(item, variables, visited, depth + 1);
                    }
                }

                continue;
            }

            WalkObject(value, variables, visited, depth + 1);
        }
    }
}
