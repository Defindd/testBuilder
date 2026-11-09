using TestBuilder.Core.Blocks;

namespace TestBuilder.Core.Execution;

/// <summary>
/// Уведомление о прогрессе выполнения одного шага.
/// </summary>
/// <param name="BlockId">Идентификатор шага.</param>
/// <param name="Title">Название шага на момент запуска.</param>
/// <param name="Status">Новое состояние шага.</param>
/// <param name="Result">Результат шага, если он уже известен.</param>
public sealed record StepProgress(
    Guid BlockId,
    string Title,
    StepStatus Status,
    BlockResult? Result);
