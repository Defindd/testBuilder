using TestBuilder.Blocks.Api;
using TestBuilder.Blocks.Elma365;
using TestBuilder.Blocks.Wait;
using TestBuilder.Core.Blocks;

namespace TestBuilder.Blocks;

/// <summary>
/// Регистрация встроенных блоков Test Builder.
/// </summary>
public static class BlockRegistryExtensions
{
    /// <summary>
    /// Регистрирует все встроенные фабрики блоков.
    /// </summary>
    /// <param name="registry">Реестр блоков.</param>
    /// <returns>Тот же реестр для цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> равен <see langword="null"/>.</exception>
    public static IBlockRegistry AddBuiltInBlocks(this IBlockRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new WaitBlockFactory());
        registry.Register(new ApiRequestBlockFactory());
        registry.Register(new CheckDataBlockFactory());

        return registry;
    }
}
