namespace TestBuilder.Core.Blocks;

/// <summary>
/// Потокобезопасный реестр фабрик блоков.
/// </summary>
public sealed class BlockRegistry : IBlockRegistry
{
    private readonly Dictionary<string, IBlockFactory> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    /// <inheritdoc />
    public void Register(IBlockFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var blockType = factory.BlockType?.Trim();
        if (string.IsNullOrWhiteSpace(blockType))
        {
            throw new ArgumentException("Фабрика блока должна задавать непустой BlockType.", nameof(factory));
        }

        lock (_sync)
        {
            if (!_factories.TryAdd(blockType, factory))
            {
                throw new InvalidOperationException(
                    $"Блок с типом '{blockType}' уже зарегистрирован. " +
                    "Типы блоков должны быть уникальными без учёта регистра.");
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<IBlockFactory> All()
    {
        lock (_sync)
        {
            return _factories.Values
                .OrderBy(factory => factory.DisplayName, StringComparer.CurrentCulture)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public ITestBlock Create(string blockType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockType);

        IBlockFactory? factory;
        lock (_sync)
        {
            _factories.TryGetValue(blockType.Trim(), out factory);
        }

        if (factory is null)
        {
            throw new KeyNotFoundException($"Тип блока '{blockType}' не зарегистрирован в BlockRegistry.");
        }

        return factory.Create()
               ?? throw new InvalidOperationException(
                   $"Фабрика '{factory.GetType().FullName}' вернула null для типа '{blockType}'.");
    }

    /// <inheritdoc />
    public IBlockFactory? Find(string blockType)
    {
        if (string.IsNullOrWhiteSpace(blockType))
        {
            return null;
        }

        lock (_sync)
        {
            _factories.TryGetValue(blockType.Trim(), out var factory);
            return factory;
        }
    }
}
