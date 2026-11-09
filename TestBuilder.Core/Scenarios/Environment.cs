namespace TestBuilder.Core.Scenarios;

/// <summary>
/// Именованный набор переменных окружения сценария.
/// </summary>
public sealed class Environment
{
    /// <summary>
    /// Название окружения (например, <c>local</c>, <c>stage</c>, <c>production</c>).
    /// </summary>
    public string Name { get; set; } = "local";

    /// <summary>
    /// Переменные окружения.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
