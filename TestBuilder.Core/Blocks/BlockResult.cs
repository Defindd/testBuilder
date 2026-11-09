namespace TestBuilder.Core.Blocks;

/// <summary>
/// Результат выполнения одного шага сценария.
/// </summary>
public sealed class BlockResult
{
    /// <summary>
    /// Успешно ли завершился шаг.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Человекочитаемое описание результата.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Длительность выполнения шага.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Значения, которые шаг публикует для отчёта и последующих проверок.
    /// </summary>
    public Dictionary<string, object> Output { get; init; } = new();

    /// <summary>
    /// Создаёт успешный результат.
    /// </summary>
    /// <param name="message">Описание результата.</param>
    /// <param name="output">Дополнительные выходные значения.</param>
    /// <returns>Успешный <see cref="BlockResult"/>.</returns>
    public static BlockResult Ok(string message = "", Dictionary<string, object>? output = null)
        => new()
        {
            Success = true,
            Message = message,
            Output = output ?? new Dictionary<string, object>(),
        };

    /// <summary>
    /// Создаёт неуспешный результат.
    /// </summary>
    /// <param name="message">Причина неуспеха.</param>
    /// <returns>Неуспешный <see cref="BlockResult"/>.</returns>
    public static BlockResult Fail(string message)
        => new()
        {
            Success = false,
            Message = message,
        };
}
