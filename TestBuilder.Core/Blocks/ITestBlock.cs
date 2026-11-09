using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;

namespace TestBuilder.Core.Blocks;

/// <summary>
/// Общий контракт шага тестового сценария.
/// Реализации должны быть сериализуемыми и иметь фабрику, зарегистрированную в <see cref="IBlockRegistry"/>.
/// </summary>
public interface ITestBlock
{
    /// <summary>
    /// Уникальный идентификатор шага в сценарии.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// Отображаемое название шага.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Признак участия шага в выполнении. Отключённые шаги пропускаются движком.
    /// </summary>
    bool Enabled { get; set; }

    /// <summary>
    /// Стабильный строковый дискриминатор типа блока (например, <c>wait</c>).
    /// </summary>
    string BlockType { get; }

    /// <summary>
    /// Выполняет шаг в заданном контексте.
    /// </summary>
    /// <param name="ctx">Контекст выполнения, включая переменные, HTTP-клиент и токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    Task<BlockResult> ExecuteAsync(ExecutionContext ctx);
}
