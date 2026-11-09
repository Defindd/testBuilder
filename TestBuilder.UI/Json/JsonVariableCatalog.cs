using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TestBuilder.Blocks.Api;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Scenarios;

namespace TestBuilder.UI.Json;

/// <summary>
/// Собирает JSON-переменные, сохранённые предыдущими API-блоками сценария.
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
            if (scenario.Blocks[index] is not ApiRequestBlock api ||
                !api.Enabled ||
                !api.SaveRequestJson ||
                api.BodyType != HttpBodyType.Json)
            {
                continue;
            }

            var name = api.SaveRequestJsonVariable?.Trim() ?? string.Empty;
            if (!VariableNameRegex.IsMatch(name))
            {
                continue;
            }

            var token = TryParseTemplate(api.Body);
            if (token is null)
            {
                continue;
            }

            var paths = new List<string>();
            CollectPaths(token, name, paths, depth: 0);
            variables[name] = new JsonVariableInfo(name, paths);
        }

        return variables.Values
            .OrderBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
