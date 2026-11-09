namespace TestBuilder.Core.Blocks;

/// <summary>
/// Реестр доступных типов блоков.
/// </summary>
public interface IBlockRegistry
{
    /// <summary>
    /// Регистрирует фабрику блока.
    /// </summary>
    /// <param name="factory">Фабрика блока.</param>
    /// <exception cref="InvalidOperationException">Фабрика с таким <see cref="IBlockFactory.BlockType"/> уже зарегистрирована.</exception>
    void Register(IBlockFactory factory);

    /// <summary>
    /// Возвращает снимок всех зарегистрированных фабрик.
    /// </summary>
    /// <returns>Фабрики в порядке отображения.</returns>
    IEnumerable<IBlockFactory> All();

    /// <summary>
    /// Создаёт экземпляр блока по строковому типу.
    /// </summary>
    /// <param name="blockType">Тип блока.</param>
    /// <returns>Новый экземпляр блока.</returns>
    /// <exception cref="KeyNotFoundException">Тип блока не зарегистрирован.</exception>
    ITestBlock Create(string blockType);

    /// <summary>
    /// Находит фабрику блока по типу.
    /// </summary>
    /// <param name="blockType">Тип блока.</param>
    /// <returns>Фабрика либо <see langword="null"/>, если тип не зарегистрирован.</returns>
    IBlockFactory? Find(string blockType);
}
