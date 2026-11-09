namespace TestBuilder.Core.Scenarios;

/// <summary>
/// Исключение валидации сценария, содержащее полный список обнаруженных ошибок.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// Создаёт исключение с набором ошибок.
    /// </summary>
    /// <param name="errors">Ошибки валидации.</param>
    /// <exception cref="ArgumentException">Список ошибок пуст.</exception>
    public ValidationException(IEnumerable<string> errors)
        : this(Materialize(errors))
    {
    }

    private ValidationException(string[] errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>
    /// Список ошибок валидации.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    private static string[] Materialize(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materialized = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .ToArray();

        if (materialized.Length == 0)
        {
            throw new ArgumentException("Список ошибок валидации не может быть пустым.", nameof(errors));
        }

        return materialized;
    }

    private static string BuildMessage(IReadOnlyCollection<string> errors)
    {
        return "Сценарий не прошёл валидацию:" + System.Environment.NewLine +
               string.Join(System.Environment.NewLine, errors.Select(error => " • " + error));
    }
}
