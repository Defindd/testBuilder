namespace TestBuilder.Core.Checks;

/// <summary>
/// Описание одной проверки значения по JSONPath.
/// </summary>
public sealed class JsonPathCheck
{
    /// <summary>
    /// JSONPath или имя поля. Для проверки HTTP-статуса используется <c>$status</c>.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Оператор сравнения.
    /// </summary>
    public CheckOperator Operator { get; set; } = CheckOperator.NotEmpty;

    /// <summary>
    /// Ожидаемое значение в строковом виде.
    /// </summary>
    public string ExpectedValue { get; set; } = string.Empty;
}
