namespace TestBuilder.Core.Blocks;

/// <summary>
/// Фабрика блоков одного типа. Используется палитрой UI, реестром и десериализацией.
/// </summary>
public interface IBlockFactory
{
    /// <summary>
    /// Стабильный строковый тип блока, совпадающий с <see cref="ITestBlock.BlockType"/>.
    /// </summary>
    string BlockType { get; }

    /// <summary>
    /// Название типа блока в палитре UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Необязательный ключ иконки для UI.
    /// </summary>
    string? IconKey { get; }

    /// <summary>
    /// Создаёт новый экземпляр блока с настройками по умолчанию.
    /// </summary>
    /// <returns>Новый экземпляр блока.</returns>
    ITestBlock Create();
}
