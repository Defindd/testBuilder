using TestBuilder.Core.Blocks;

namespace TestBuilder.Blocks.Wait;

/// <summary>
/// Фабрика блока ожидания.
/// </summary>
public sealed class WaitBlockFactory : IBlockFactory
{
    /// <inheritdoc />
    public string BlockType => WaitBlock.TypeName;

    /// <inheritdoc />
    public string DisplayName => "Ожидание";

    /// <inheritdoc />
    public string? IconKey => "wait";

    /// <inheritdoc />
    public ITestBlock Create() => new WaitBlock();
}
