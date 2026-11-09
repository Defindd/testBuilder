using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using TestBuilder.Core.Execution;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;

namespace TestBuilder.Tests.TestDoubles;

/// <summary>
/// Режим тестового блока, проверяющий сериализацию enum строками.
/// </summary>
public enum FakeMode
{
    /// <summary>Первый режим.</summary>
    First,

    /// <summary>Второй режим.</summary>
    Second,
}

/// <summary>
/// Блок-заглушка для тестов Core до появления реальных блоков.
/// </summary>
public sealed class FakeBlock : ITestBlock, IValidatableBlock
{
    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    [BlockProperty("Название", 0)]
    public string Title { get; set; } = "Фейковый шаг";

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public string BlockType => "fake";

    /// <summary>Задержка выполнения.</summary>
    [BlockProperty("Задержка", 1)]
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>Тестовый режим.</summary>
    [BlockProperty("Режим", 2)]
    public FakeMode Mode { get; set; } = FakeMode.First;

    /// <summary>Таблица ключ-значение.</summary>
    [BlockProperty("Элементы", 3, PropertyEditorType.KeyValueTable)]
    public List<KeyValueItem> Items { get; set; } = new();

    /// <summary>Таблица проверок.</summary>
    [BlockProperty("Проверки", 4, PropertyEditorType.JsonPathCheckTable)]
    public List<JsonPathCheck> Checks { get; set; } = new();

    /// <summary>Требовать ли заполненный URL при валидации.</summary>
    public bool RequireUrl { get; set; }

    /// <summary>Тестовый URL.</summary>
    public string Url { get; set; } = string.Empty;

    /// <inheritdoc />
    public async Task<BlockResult> ExecuteAsync(ExecutionContext ctx)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, ctx.CancellationToken);
        }

        return BlockResult.Ok("Фейковый блок выполнен.");
    }

    /// <inheritdoc />
    public IEnumerable<string> Validate()
    {
        if (RequireUrl && string.IsNullOrWhiteSpace(Url))
        {
            yield return "Url обязателен.";
        }
    }
}

/// <summary>
/// Фабрика <see cref="FakeBlock"/>.
/// </summary>
public sealed class FakeBlockFactory : IBlockFactory
{
    /// <inheritdoc />
    public string BlockType => "fake";

    /// <inheritdoc />
    public string DisplayName => "Фейковый блок";

    /// <inheritdoc />
    public string? IconKey => null;

    /// <inheritdoc />
    public ITestBlock Create() => new FakeBlock();
}
