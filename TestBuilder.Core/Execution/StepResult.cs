namespace TestBuilder.Core.Execution;

/// <summary>
/// Итог выполнения одного шага, сохраняемый в отчёте.
/// </summary>
public sealed class StepResult
{
    /// <summary>
    /// Идентификатор шага.
    /// </summary>
    public Guid BlockId { get; init; }

    /// <summary>
    /// Название шага.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Тип блока.
    /// </summary>
    public string BlockType { get; init; } = string.Empty;

    /// <summary>
    /// Итоговый статус шага.
    /// </summary>
    public StepStatus Status { get; init; }

    /// <summary>
    /// Фактическая длительность выполнения.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Сообщение об успехе или причина ошибки.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Сериализованные выходные данные шага (JSON), если они есть.
    /// </summary>
    public string? OutputJson { get; init; }
}
