namespace TestBuilder.Core.Blocks;

/// <summary>
/// Необязательный контракт блока, умеющего проверять собственные настройки.
/// Позволяет Core не зависеть от конкретных реализаций блоков.
/// </summary>
public interface IValidatableBlock
{
    /// <summary>
    /// Возвращает список ошибок конфигурации блока. Пустой список означает, что блок корректен.
    /// </summary>
    /// <returns>Человекочитаемые ошибки конфигурации.</returns>
    IEnumerable<string> Validate();
}
