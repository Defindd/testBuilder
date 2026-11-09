using TestBuilder.Core.Blocks;

namespace TestBuilder.Core.Scenarios;

/// <summary>
/// Тестовый сценарий, сохраняемый в файл <c>.tbtest</c>.
/// </summary>
public sealed class TestScenario
{
    /// <summary>
    /// Название сценария.
    /// </summary>
    public string Name { get; set; } = "Новый сценарий";

    /// <summary>
    /// Окружение и его переменные.
    /// </summary>
    public Environment Environment { get; set; } = new();

    /// <summary>
    /// Статические переменные сценария. Переопределяют переменные окружения с теми же именами.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Упорядоченный список шагов.
    /// </summary>
    public List<ITestBlock> Blocks { get; set; } = new();
}
