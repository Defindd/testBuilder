namespace TestBuilder.Core.Blocks;

/// <summary>
/// Тип редактора, который инспектор свойств должен использовать для свойства блока.
/// </summary>
public enum PropertyEditorType
{
    /// <summary>Редактор выбирается автоматически по типу CLR-свойства.</summary>
    Auto,

    /// <summary>Многострочное текстовое поле.</summary>
    MultilineText,

    /// <summary>Таблица «ключ — значение».</summary>
    KeyValueTable,

    /// <summary>Таблица проверок JSONPath.</summary>
    JsonPathCheckTable,

    /// <summary>Поле для секретов с маскировкой ввода.</summary>
    Secret,
}

/// <summary>
/// Метаданные свойства блока для автоматического построения инспектора свойств.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class BlockPropertyAttribute : Attribute
{
    /// <summary>
    /// Создаёт описание свойства блока.
    /// </summary>
    /// <param name="displayName">Подпись свойства в UI.</param>
    /// <param name="order">Порядок отображения; меньшие значения выводятся раньше.</param>
    /// <param name="editor">Тип редактора.</param>
    /// <param name="description">Подсказка для пользователя.</param>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> пуст.</exception>
    public BlockPropertyAttribute(
        string displayName,
        int order = 0,
        PropertyEditorType editor = PropertyEditorType.Auto,
        string description = "")
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Отображаемое имя свойства не может быть пустым.", nameof(displayName));
        }

        DisplayName = displayName;
        Order = order;
        Editor = editor;
        Description = description ?? string.Empty;
    }

    /// <summary>
    /// Подпись свойства в UI.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Порядок отображения свойства.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Тип редактора.
    /// </summary>
    public PropertyEditorType Editor { get; }

    /// <summary>
    /// Подсказка для пользователя.
    /// </summary>
    public string Description { get; }
}
