namespace TestBuilder.Core.Checks;

/// <summary>
/// Операторы проверки значения, полученного по JSONPath.
/// </summary>
public enum CheckOperator
{
    /// <summary>Значение равно ожидаемому.</summary>
    Equals,

    /// <summary>Значение не равно ожидаемому.</summary>
    NotEquals,

    /// <summary>Строка или коллекция содержит ожидаемое значение.</summary>
    Contains,

    /// <summary>Строка или коллекция не содержит ожидаемое значение.</summary>
    NotContains,

    /// <summary>Значение соответствует регулярному выражению.</summary>
    MatchesRegex,

    /// <summary>Значение отсутствует, пустое или является пустой коллекцией.</summary>
    Empty,

    /// <summary>Значение присутствует и не пустое.</summary>
    NotEmpty,

    /// <summary>Числовое значение больше ожидаемого.</summary>
    Greater,

    /// <summary>Числовое значение меньше ожидаемого.</summary>
    Less,

    /// <summary>Значение входит в список ожидаемых значений через запятую.</summary>
    In,

    /// <summary>Числовое значение входит в диапазон <c>min..max</c> включительно.</summary>
    Between,

    /// <summary>Количество элементов равно ожидаемому.</summary>
    CountEquals,

    /// <summary>Количество элементов больше ожидаемого.</summary>
    CountGreater,
}
