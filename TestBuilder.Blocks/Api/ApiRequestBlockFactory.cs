using TestBuilder.Core.Blocks;

namespace TestBuilder.Blocks.Api;

/// <summary>
/// Фабрика блока отправки API-запроса.
/// </summary>
public sealed class ApiRequestBlockFactory : IBlockFactory
{
    /// <inheritdoc />
    public string BlockType => ApiRequestBlock.TypeName;

    /// <inheritdoc />
    public string DisplayName => "Отправка API запроса";

    /// <inheritdoc />
    public string? IconKey => "api";

    /// <inheritdoc />
    public ITestBlock Create() => new ApiRequestBlock();
}
