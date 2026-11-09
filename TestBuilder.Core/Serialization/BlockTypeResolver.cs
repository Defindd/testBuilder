using TestBuilder.Core.Blocks;

namespace TestBuilder.Core.Serialization;

/// <summary>
/// Разрешает строковый тип блока в CLR-тип через <see cref="IBlockRegistry"/>.
/// </summary>
public sealed class BlockTypeResolver
{
    private readonly IBlockRegistry _registry;
    private readonly Dictionary<string, Type> _typeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    /// <summary>
    /// Создаёт резолвер.
    /// </summary>
    /// <param name="registry">Реестр фабрик блоков.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> равен <see langword="null"/>.</exception>
    public BlockTypeResolver(IBlockRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Проверяет, зарегистрирован ли тип блока.
    /// </summary>
    /// <param name="blockType">Строковый тип блока.</param>
    /// <returns><see langword="true"/>, если тип зарегистрирован.</returns>
    public bool IsRegistered(string blockType)
    {
        return !string.IsNullOrWhiteSpace(blockType) && _registry.Find(blockType) is not null;
    }

    /// <summary>
    /// Возвращает CLR-тип блока для дискриминатора <c>$type</c>.
    /// </summary>
    /// <param name="blockType">Строковый тип блока.</param>
    /// <returns>CLR-тип конкретного блока.</returns>
    /// <exception cref="ArgumentException">Тип блока пуст.</exception>
    /// <exception cref="KeyNotFoundException">Тип блока не зарегистрирован.</exception>
    public Type ResolveClrType(string blockType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);

        lock (_sync)
        {
            if (_typeCache.TryGetValue(blockType, out var cachedType))
            {
                return cachedType;
            }
        }

        // Фабрика — единственный контракт, который знает CLR-тип создаваемого блока.
        var block = _registry.Create(blockType);
        var resolvedType = block.GetType();

        lock (_sync)
        {
            _typeCache[blockType] = resolvedType;
        }

        return resolvedType;
    }

    /// <summary>
    /// Создаёт экземпляр блока по строковому типу.
    /// </summary>
    /// <param name="blockType">Строковый тип блока.</param>
    /// <returns>Новый экземпляр блока.</returns>
    public ITestBlock Create(string blockType)
    {
        return _registry.Create(blockType);
    }
}
