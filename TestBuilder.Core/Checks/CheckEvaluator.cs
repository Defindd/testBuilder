using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TestBuilder.Core.Checks;

/// <summary>
/// Вычисляет результат одной проверки <see cref="JsonPathCheck"/> для токена JSON.
/// </summary>
public static class CheckEvaluator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private const int MaxDisplayLength = 200;

    /// <summary>
    /// Проверяет значение токена согласно оператору проверки.
    /// </summary>
    /// <param name="token">Значение, полученное по JSONPath, либо <see langword="null"/>.</param>
    /// <param name="check">Описание проверки.</param>
    /// <returns>Признак успеха и диагностическое сообщение.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="check"/> равен <see langword="null"/>.</exception>
    public static (bool success, string message) Evaluate(JToken? token, JsonPathCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        var path = string.IsNullOrWhiteSpace(check.Path) ? "$" : check.Path.Trim();
        var expected = check.ExpectedValue ?? string.Empty;

        if (IsMissing(token))
        {
            return check.Operator switch
            {
                CheckOperator.Empty => Success(path, check.Operator, "значение отсутствует"),
                CheckOperator.NotEmpty => Failure(path, check.Operator, expected, "значение отсутствует"),
                _ => Failure(path, check.Operator, expected, "значение отсутствует"),
            };
        }

        try
        {
            return check.Operator switch
            {
                CheckOperator.Equals => EvaluateEquals(token!, path, check.Operator, expected),
                CheckOperator.NotEquals => EvaluateNotEquals(token!, path, check.Operator, expected),
                CheckOperator.Contains => EvaluateContains(token!, path, check.Operator, expected),
                CheckOperator.NotContains => EvaluateNotContains(token!, path, check.Operator, expected),
                CheckOperator.MatchesRegex => EvaluateRegex(token!, path, expected),
                CheckOperator.Empty => EvaluateEmpty(token!, path, emptyExpected: true),
                CheckOperator.NotEmpty => EvaluateEmpty(token!, path, emptyExpected: false),
                CheckOperator.Greater => EvaluateNumericComparison(token!, path, check.Operator, expected, greater: true),
                CheckOperator.Less => EvaluateNumericComparison(token!, path, check.Operator, expected, greater: false),
                CheckOperator.In => EvaluateIn(token!, path, expected),
                CheckOperator.Between => EvaluateBetween(token!, path, expected),
                CheckOperator.CountEquals => EvaluateCount(token!, path, check.Operator, expected, greater: false),
                CheckOperator.CountGreater => EvaluateCount(token!, path, check.Operator, expected, greater: true),
                _ => Failure(path, check.Operator, expected, "оператор не поддерживается"),
            };
        }
        catch (RegexMatchTimeoutException)
        {
            return Failure(path, check.Operator, expected, "превышено время выполнения регулярного выражения");
        }
        catch (ArgumentException ex)
        {
            return Failure(path, check.Operator, expected, $"некорректный аргумент проверки: {ex.Message}");
        }
    }

    private static (bool success, string message) EvaluateEquals(
        JToken token,
        string path,
        CheckOperator checkOperator,
        string expected)
    {
        return AreEqual(token, expected)
            ? Success(path, checkOperator, Describe(token))
            : Failure(path, checkOperator, expected, Describe(token));
    }

    private static (bool success, string message) EvaluateNotEquals(
        JToken token,
        string path,
        CheckOperator checkOperator,
        string expected)
    {
        return !AreEqual(token, expected)
            ? Success(path, checkOperator, Describe(token))
            : Failure(path, checkOperator, expected, Describe(token));
    }

    private static (bool success, string message) EvaluateContains(
        JToken token,
        string path,
        CheckOperator checkOperator,
        string expected)
    {
        return Contains(token, expected)
            ? Success(path, checkOperator, Describe(token))
            : Failure(path, checkOperator, expected, Describe(token));
    }

    private static (bool success, string message) EvaluateNotContains(
        JToken token,
        string path,
        CheckOperator checkOperator,
        string expected)
    {
        return !Contains(token, expected)
            ? Success(path, checkOperator, Describe(token))
            : Failure(path, checkOperator, expected, Describe(token));
    }

    private static (bool success, string message) EvaluateRegex(
        JToken token,
        string path,
        string expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return Failure(path, CheckOperator.MatchesRegex, expected, "регулярное выражение не задано");
        }

        var actual = ToInvariantString(token);
        return Regex.IsMatch(actual, expected, RegexOptions.CultureInvariant, RegexTimeout)
            ? Success(path, CheckOperator.MatchesRegex, actual)
            : Failure(path, CheckOperator.MatchesRegex, expected, actual);
    }

    private static (bool success, string message) EvaluateEmpty(
        JToken token,
        string path,
        bool emptyExpected)
    {
        var isEmpty = IsEmpty(token);
        var passed = emptyExpected ? isEmpty : !isEmpty;

        return passed
            ? Success(path, emptyExpected ? CheckOperator.Empty : CheckOperator.NotEmpty, Describe(token))
            : Failure(
                path,
                emptyExpected ? CheckOperator.Empty : CheckOperator.NotEmpty,
                emptyExpected ? "пустое значение" : "непустое значение",
                Describe(token));
    }

    private static (bool success, string message) EvaluateNumericComparison(
        JToken token,
        string path,
        CheckOperator checkOperator,
        string expected,
        bool greater)
    {
        if (!TryGetDecimal(token, out var actual))
        {
            return Failure(path, checkOperator, expected, $"значение не является числом: {Describe(token)}");
        }

        if (!decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedNumber))
        {
            return Failure(path, checkOperator, expected, "ожидаемое значение не является числом");
        }

        var passed = greater ? actual > expectedNumber : actual < expectedNumber;
        return passed
            ? Success(path, checkOperator, actual.ToString(CultureInfo.InvariantCulture))
            : Failure(path, checkOperator, expected, actual.ToString(CultureInfo.InvariantCulture));
    }

    private static (bool success, string message) EvaluateIn(
        JToken token,
        string path,
        string expected)
    {
        var candidates = SplitList(expected);
        if (candidates.Length == 0)
        {
            return Failure(path, CheckOperator.In, expected, "список ожидаемых значений пуст");
        }

        return candidates.Any(candidate => AreEqual(token, candidate))
            ? Success(path, CheckOperator.In, Describe(token))
            : Failure(path, CheckOperator.In, expected, Describe(token));
    }

    private static (bool success, string message) EvaluateBetween(
        JToken token,
        string path,
        string expected)
    {
        var parts = expected.Split("..", StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryParseDecimal(parts[0], out var minimum) ||
            !TryParseDecimal(parts[1], out var maximum))
        {
            return Failure(path, CheckOperator.Between, expected, "ожидается диапазон вида 'min..max'");
        }

        if (minimum > maximum)
        {
            return Failure(path, CheckOperator.Between, expected, "нижняя граница диапазона больше верхней");
        }

        if (!TryGetDecimal(token, out var actual))
        {
            return Failure(path, CheckOperator.Between, expected, $"значение не является числом: {Describe(token)}");
        }

        return actual >= minimum && actual <= maximum
            ? Success(path, CheckOperator.Between, actual.ToString(CultureInfo.InvariantCulture))
            : Failure(path, CheckOperator.Between, expected, actual.ToString(CultureInfo.InvariantCulture));
    }

    private static (bool success, string message) EvaluateCount(
        JToken token,
        string path,
        CheckOperator checkOperator,
        string expected,
        bool greater)
    {
        if (!TryGetCount(token, out var count))
        {
            return Failure(path, checkOperator, expected, "значение не является коллекцией или строкой");
        }

        if (!int.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedCount) ||
            expectedCount < 0)
        {
            return Failure(path, checkOperator, expected, "ожидаемое количество должно быть неотрицательным целым числом");
        }

        var passed = greater ? count > expectedCount : count == expectedCount;
        return passed
            ? Success(path, checkOperator, count.ToString(CultureInfo.InvariantCulture))
            : Failure(path, checkOperator, expected, count.ToString(CultureInfo.InvariantCulture));
    }

    private static bool AreEqual(JToken token, string expected)
    {
        if (token is JValue value)
        {
            var actualText = ToInvariantString(value.Value);
            if (TryParseDecimal(actualText, out var actualNumber) &&
                TryParseDecimal(expected, out var expectedNumber))
            {
                return actualNumber == expectedNumber;
            }

            if (value.Value is bool actualBoolean &&
                bool.TryParse(expected, out var expectedBoolean))
            {
                return actualBoolean == expectedBoolean;
            }

            return string.Equals(actualText, expected, StringComparison.Ordinal);
        }

        if (token is JContainer)
        {
            try
            {
                var expectedToken = JToken.Parse(expected);
                return JToken.DeepEquals(token, expectedToken);
            }
            catch (JsonException)
            {
                return string.Equals(
                    token.ToString(Formatting.None),
                    expected.Trim(),
                    StringComparison.Ordinal);
            }
        }

        return string.Equals(ToInvariantString(token), expected, StringComparison.Ordinal);
    }

    private static bool Contains(JToken token, string expected)
    {
        switch (token)
        {
            case JArray array:
                return array.Any(item => AreEqual(item, expected));

            case JObject jsonObject:
                return jsonObject.Properties().Any(property =>
                    string.Equals(property.Name, expected, StringComparison.Ordinal) ||
                    AreEqual(property.Value, expected));

            case JValue value:
                return ToInvariantString(value.Value)
                    .Contains(expected, StringComparison.Ordinal);

            default:
                return ToInvariantString(token)
                    .Contains(expected, StringComparison.Ordinal);
        }
    }

    private static bool IsMissing(JToken? token)
    {
        return token is null ||
               token.Type is JTokenType.Null or JTokenType.Undefined;
    }

    private static bool IsEmpty(JToken token)
    {
        return token switch
        {
            JValue value => string.IsNullOrEmpty(ToInvariantString(value.Value)),
            JArray array => array.Count == 0,
            JObject jsonObject => !jsonObject.Properties().Any(),
            _ => false,
        };
    }

    private static bool TryGetDecimal(JToken token, out decimal number)
    {
        return TryParseDecimal(ToInvariantString(token), out number);
    }

    private static bool TryParseDecimal(string? value, out decimal number)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool TryGetCount(JToken token, out int count)
    {
        switch (token)
        {
            case JArray array:
                count = array.Count;
                return true;

            case JObject jsonObject:
                count = jsonObject.Count;
                return true;

            case JValue value when value.Value is string text:
                count = text.Length;
                return true;

            default:
                count = 0;
                return false;
        }
    }

    private static string[] SplitList(string expected)
    {
        return expected
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ToInvariantString(JToken token)
    {
        return token switch
        {
            JValue value => ToInvariantString(value.Value),
            JArray or JObject => token.ToString(Formatting.None),
            _ => token.ToString(Formatting.None),
        };
    }

    private static string ToInvariantString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            bool boolean => boolean ? "true" : "false",
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string Describe(JToken token)
    {
        var text = ToInvariantString(token);
        return text.Length <= MaxDisplayLength
            ? text
            : text[..MaxDisplayLength] + "…";
    }

    private static (bool success, string message) Success(
        string path,
        CheckOperator checkOperator,
        string actual)
    {
        return (true, $"Проверка '{path}' ({checkOperator}) пройдена. Значение: {actual}.");
    }

    private static (bool success, string message) Failure(
        string path,
        CheckOperator checkOperator,
        string expected,
        string actual)
    {
        var expectedDisplay = string.IsNullOrEmpty(expected) ? "<пусто>" : expected;
        return (
            false,
            $"Проверка '{path}' ({checkOperator}) не пройдена. Ожидалось: {expectedDisplay}; фактически: {actual}.");
    }
}
