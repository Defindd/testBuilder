namespace TestBuilder.Core.Checks;

/// <summary>
/// Элемент таблицы «ключ — значение» для заголовков, query-параметров и форм.
/// </summary>
public sealed class KeyValueItem
{
    /// <summary>
    /// Ключ.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Значение.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
