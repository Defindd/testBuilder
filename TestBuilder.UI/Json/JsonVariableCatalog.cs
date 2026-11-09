using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestBuilder.Blocks.Api;
using TestBuilder.Blocks.Elma365;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using TestBuilder.Core.Scenarios;

namespace TestBuilder.UI.Json;

/// <summary>
/// Собирает JSON-переменные, сохранённые предыдущими блоками сценария.
/// Для ответов API и объектов Elma365 структура берётся из JSONPath-проверок,
/// а для JSON запроса — из тела запроса.
/// </summary>
public static class JsonVariableCatalog
{
    private static readonly Regex TemplateTokenRegex = new(
        @"\{\{.*?\}\}|\{\$.*?\}",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex VariableNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SegmentRegex = new(
        @"^[\w\-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ArrayIndexRegex = new(
        @"\[(\d+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedSegmentRegex = new(
        @"\['([^']+)'\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int MaxDepth = 16;
    private const int MaxPaths = 1000;

    /// <summary>
    /// Возвращает переменные, сохранённые блоками до указанной позиции.
    /// </summary>
    /// <param name="scenario">Сценарий.</param>
    /// <param name="beforeBlockIndex">Индекс текущего блока.</param>
    /// <returns>Уникальные JSON-переменные по имени.</returns>
    public static IReadOnlyList<JsonVariableInfo> Build(
        TestScenario scenario,
        int beforeBlockIndex)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var variables = new Dictionary<string, JsonVariableInfo>(StringComparer.OrdinalIgnoreCase);
        var lastIndex = Math.Min(beforeBlockIndex, scenario.Blocks.Count);

        for (var index = 0; index < lastIndex; index++)
        {
            var block = scenario.Blocks[index];
            if (!block.Enabled)
            {
                continue;
            }

            if (block is ApiRequestBlock api)
            {
                AddRequestJson(api, variables);
                AddJsonVariable(
                    api.SaveResponseVariable,
                    BuildPathsFromChecks(api.SaveResponseVariable, api.ResponseChecks),
                    variables);
                continue;
            }

            if (block is CheckDataBlock checkData)
            {
                AddJsonVariable(
                    checkData.SaveObjectVariable,
                    BuildPathsFromChecks(checkData.SaveObjectVariable, checkData.FieldChecks),
                    variables);
            }
        }

        return variables.Values
            .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddRequestJson(
        ApiRequestBlock api,
        IDictionary<string, JsonVariableInfo> variables)
    {
        if (!api.SaveRequestJson || api.BodyType != HttpBodyType.Json)
        {
            return;
        }

        var name = api.SaveRequestJsonVariable?.Trim() ?? string.Empty;
        if (!VariableNameRegex.IsMatch(name))
        {
            return;
        }

        var token = TryParseTemplate(api.Body);
        if (token is null)
        {
            return;
        }

        var paths = new List<string>();
        CollectPaths(token, name, paths, depth: 0);
        AddJsonVariable(name, paths, variables);
    }

    private static void AddJsonVariable(
        string? rawName,
        IReadOnlyList<string>? paths,
        IDictionary<string, JsonVariableInfo> variables)
    {
        var name = rawName?.Trim() ?? string.Empty;
        if (!VariableNameRegex.IsMatch(name) || paths is null)
        {
            return;
        }

        if (variables.TryGetValue(name, out var existing))
        {
            var merged = existing.Paths
                .Concat(paths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            variables[name] = new JsonVariableInfo(name, merged);
            return;
        }

        variables[name] = new JsonVariableInfo(name, paths);
    }

    private static IReadOnlyList<string> BuildPathsFromChecks(
        string? rawName,
        IEnumerable<JsonPathCheck>? checks)
    {
        var name = rawName?.Trim() ?? string.Empty;
        if (!VariableNameRegex.IsMatch(name))
        {
            return Array.Empty<string>();
        }

        var paths = new List<string> { name };
        foreach (var check in checks ?? Enumerable.Empty<JsonPathCheck>())
        {
            var segments = NormalizeCheckPath(check.Path);
            if (segments is null)
            {
                continue;
            }

            var current = name;
            foreach (var segment in segments)
            {
                current += "." + segment;
                if (!paths.Contains(current, StringComparer.OrdinalIgnoreCase))
                {
                    paths.Add(current);
                }
            }
        }

        return paths;
    }

    private static string[]? NormalizeCheckPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var value = path.Trim();
        if (value.Equals("$status", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (value.StartsWith("$.", StringComparison.Ordinal))
        {
            value = value[2..];
        }
        else if (value.StartsWith('$'))
        {
            value = value[1..];
        }

        if (value.StartsWith('.'))
        {
            value = value[1..];
        }

        if (value.Length == 0 || value.Contains('?'))
        {
            return null;
        }

        value = ArrayIndexRegex.Replace(value, ".$1");
        value = QuotedSegmentRegex.Replace(value, ".$1");

        var segments = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(SegmentRegex.IsMatch)
            ? segments
            : null;
    }

    private static JToken? TryParseTemplate(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        // Шаблоны могут стоять как в строках, так и вместо чисел/объектов.
        // null сохраняет валидность JSON и не мешает определить структуру.
        var sanitized = TemplateTokenRegex.Replace(body, "null");

        try
        {
            return JToken.Parse(sanitized);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void CollectPaths(
        JToken token,
        string path,
        ICollection<string> paths,
        int depth)
    {
        if (paths.Count >= MaxPaths || depth > MaxDepth)
        {
            return;
        }

        paths.Add(path);

        switch (token)
        {
            case JObject jsonObject:
                foreach (var property in jsonObject.Properties())
                {
                    if (SegmentRegex.IsMatch(property.Name))
                    {
                        CollectPaths(property.Value, $"{path}.{property.Name}", paths, depth + 1);
                    }
                }

                break;

            case JArray array when array.Count > 0:
                CollectPaths(array[0], $"{path}.0", paths, depth + 1);
                break;
        }
    }
}
