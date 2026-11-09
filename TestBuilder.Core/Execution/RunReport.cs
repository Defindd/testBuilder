namespace TestBuilder.Core.Execution;

/// <summary>
/// Отчёт о выполнении сценария.
/// </summary>
public sealed class RunReport
{
    /// <summary>
    /// Название сценария.
    /// </summary>
    public string ScenarioName { get; init; } = string.Empty;

    /// <summary>
    /// Время начала запуска в UTC.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Суммарная длительность запуска.
    /// </summary>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Результаты выполненных шагов в порядке выполнения.
    /// </summary>
    public List<StepResult> Steps { get; init; } = new();

    /// <summary>
    /// Признак успешного запуска: все выполненные шаги завершились успешно.
    /// </summary>
    public bool AllPassed => Steps.All(step => step.Status == StepStatus.Success);
}
