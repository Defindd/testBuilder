using TestBuilder.Core.Blocks;

namespace TestBuilder.Blocks.Elma365;

/// <summary>
/// Фабрика блока проверки данных Elma365.
/// </summary>
public sealed class CheckDataBlockFactory : IBlockFactory
{
    /// <inheritdoc />
    public string BlockType => CheckDataBlock.TypeName;

    /// <inheritdoc />
    public string DisplayName => "Проверить данные (Elma365)";

    /// <inheritdoc />
    public string? IconKey => "database";

    /// <inheritdoc />
    public ITestBlock Create() => new CheckDataBlock();
}
