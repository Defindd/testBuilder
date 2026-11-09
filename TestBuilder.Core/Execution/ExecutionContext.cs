using Microsoft.Extensions.Logging;
using TestBuilder.Core.Templating;

namespace TestBuilder.Core.Execution;

/// <summary>
/// Контекст одного запуска сценария. Создаётся на запуск и не переиспользуется между запусками.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>
    /// Переменные сценария. Блоки могут читать и дополнять словарь во время выполнения.
    /// </summary>
    public Dictionary<string, object> Variables { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// HTTP-клиент, используемый сетевыми блоками.
    /// </summary>
    public required HttpClient HttpClient { get; init; }

    /// <summary>
    /// Логгер запуска.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// Токен отмены всего запуска.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Движок шаблонов, общий для всех блоков запуска.
    /// </summary>
    public required TemplateEngine Templates { get; init; }
}
