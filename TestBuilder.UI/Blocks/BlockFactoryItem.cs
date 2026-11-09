using TestBuilder.Core.Blocks;

namespace TestBuilder.UI.Blocks;

/// <summary>
/// Обёртка фабрики блока для отображения в палитре.
/// </summary>
public sealed class BlockFactoryItem
{
    /// <summary>
    /// Создаёт элемент палитры.
    /// </summary>
    /// <param name="factory">Фабрика блока.</param>
    public BlockFactoryItem(IBlockFactory factory)
    {
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Фабрика блока.
    /// </summary>
    public IBlockFactory Factory { get; }

    /// <summary>
    /// Отображаемое имя типа блока.
    /// </summary>
    public string DisplayName => Factory.DisplayName;

    /// <summary>
    /// Строковый тип блока.
    /// </summary>
    public string BlockType => Factory.BlockType;

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}
