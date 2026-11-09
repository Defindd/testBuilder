using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Execution;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;

namespace TestBuilder.Blocks.Wait;

/// <summary>
/// Режим блока ожидания.
/// </summary>
public enum WaitMode
{
    /// <summary>Фиксированная пауза.</summary>
    Fixed,

    /// <summary>Ожидание до выполнения условия.</summary>
    UntilCondition,
}

/// <summary>
/// Блок ожидания: фиксированная пауза либо опрос простого условия.
/// </summary>
public sealed class WaitBlock : ITestBlock, IValidatableBlock
{
    /// <summary>
    /// Стабильный строковый тип блока.
    /// </summary>
    public const string TypeName = "wait";

    private static readonly Regex ConditionRegex = new(
        @"^(?<left>.*?)\s*(?<op>==|!=|>=|<=|>|<)\s*(?<right>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <inheritdoc />
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <inheritdoc />
    [BlockProperty("Название", 0)]
    public string Title { get; set; } = "Ожидание";

    /// <inheritdoc />
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public string BlockType => TypeName;

    /// <summary>
    /// Режим ожидания.
    /// </summary>
    [BlockProperty("Режим", 1)]
    public WaitMode Mode { get; set; } = WaitMode.Fixed;

    /// <summary>
    /// Фиксированная длительность ожидания.
    /// </summary>
    [BlockProperty("Длительность", 2)]
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Условие ожидания. Поддерживаются операторы <c>==</c>, <c>!=</c>, <c>&gt;</c>,
    /// <c>&lt;</c>, <c>&gt;=</c>, <c>&lt;=</c>.
    /// </summary>
    [BlockProperty(
        "Условие",
        3,
        PropertyEditorType.MultilineText,
        "Например: {{lastResponse.status}} == 200")]
    public string ConditionExpression { get; set; } = string.Empty;

    /// <summary>
    /// Максимальное время ожидания условия.
    /// </summary>
    [BlockProperty("Макс. ожидание", 4)]
    public TimeSpan MaxWait { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Интервал между проверками условия.
    /// </summary>
    [BlockProperty("Интервал опроса", 5)]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Случайный разброс фиксированной паузы в миллисекундах.
    /// </summary>
    [BlockProperty("Джиттер, мс", 6)]
    public int JitterMs { get; set; }

    /// <inheritdoc />
    public async Task<BlockResult> ExecuteAsync(ExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        return Mode switch
        {
            WaitMode.Fixed => await ExecuteFixedAsync(ctx).ConfigureAwait(false),
            WaitMode.UntilCondition => await ExecuteUntilConditionAsync(ctx).ConfigureAwait(false),
            _ => CreateResult(
                success: false,
                message: $"Неизвестный режим ожидания: {Mode}.",
                elapsed: TimeSpan.Zero,
                output: new Dictionary<string, object>()),
        };
    }

    /// <inheritdoc />
    public IEnumerable<string> Validate()
    {
        if (Duration < TimeSpan.Zero)
        {
            yield return "Длительность не может быть отрицательной.";
        }

        if (JitterMs < 0)
        {
            yield return "Джиттер не может быть отрицательным.";
        }

        if (Mode != WaitMode.UntilCondition)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(ConditionExpression))
        {
            yield return "Для режима UntilCondition необходимо задать условие.";
        }
        else if (!ConditionRegex.IsMatch(ConditionExpression))
        {
            yield return "Условие должно содержать один из операторов: ==, !=, >, <, >=, <=.";
        }

        if (MaxWait <= TimeSpan.Zero)
        {
            yield return "Максимальное ожидание должно быть больше нуля.";
        }

        if (PollInterval <= TimeSpan.Zero)
        {
            yield return "Интервал опроса должен быть больше нуля.";
        }
    }

    private async Task<BlockResult> ExecuteFixedAsync(ExecutionContext ctx)
    {
        var jitter = JitterMs > 0
            ? Random.Shared.Next(-JitterMs, JitterMs + 1)
            : 0;

        var delay = Duration + TimeSpan.FromMilliseconds(jitter);
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var stopwatch = Stopwatch.StartNew();
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ctx.CancellationToken).ConfigureAwait(false);
        }

        stopwatch.Stop();

        return CreateResult(
            success: true,
            message: $"Ожидание {FormatDuration(delay)} завершено.",
            elapsed: stopwatch.Elapsed,
            output: new Dictionary<string, object>
            {
                ["mode"] = Mode.ToString(),
                ["requestedMs"] = delay.TotalMilliseconds,
            });
    }

    private async Task<BlockResult> ExecuteUntilConditionAsync(ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ConditionExpression))
        {
            return CreateResult(
                success: false,
                message: "Условие ожидания не задано.",
                elapsed: TimeSpan.Zero,
                output: new Dictionary<string, object>());
        }

        if (!ConditionRegex.IsMatch(ConditionExpression))
        {
            return CreateResult(
                success: false,
                message: "Не удалось разобрать условие. Поддерживаются операторы ==, !=, >, <, >=, <=.",
                elapsed: TimeSpan.Zero,
                output: new Dictionary<string, object>());
        }

        if (MaxWait <= TimeSpan.Zero)
        {
            return CreateResult(
                success: false,
                message: "Максимальное ожидание должно быть больше нуля.",
                elapsed: TimeSpan.Zero,
                output: new Dictionary<string, object>());
        }

        if (PollInterval <= TimeSpan.Zero)
        {
            return CreateResult(
                success: false,
                message: "Интервал опроса должен быть больше нуля.",
                elapsed: TimeSpan.Zero,
                output: new Dictionary<string, object>());
        }

        var stopwatch = Stopwatch.StartNew();
        var lastResolved = ConditionExpression;

        while (true)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var evaluation = EvaluateCondition(ctx);
            lastResolved = evaluation.ResolvedText;

            if (evaluation.Error is not null)
            {
                stopwatch.Stop();
                return CreateResult(
                    success: false,
                    message: evaluation.Error,
                    elapsed: stopwatch.Elapsed,
                    output: new Dictionary<string, object> { ["resolved"] = lastResolved });
            }

            if (evaluation.IsTrue)
            {
                stopwatch.Stop();
                ctx.Logger.LogDebug(
                    "WaitBlock: условие '{Expression}' стало истинным за {Duration}.",
                    ConditionExpression,
                    stopwatch.Elapsed);

                return CreateResult(
                    success: true,
                    message: $"Условие стало истинным за {FormatDuration(stopwatch.Elapsed)}.",
                    elapsed: stopwatch.Elapsed,
                    output: new Dictionary<string, object> { ["resolved"] = lastResolved });
            }

            var remaining = MaxWait - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                stopwatch.Stop();
                return CreateResult(
                    success: false,
                    message:
                    $"Условие '{ConditionExpression}' не стало истинным за {FormatDuration(MaxWait)}. " +
                    $"Последнее значение: '{lastResolved}'.",
                    elapsed: stopwatch.Elapsed,
                    output: new Dictionary<string, object> { ["resolved"] = lastResolved });
            }

            var delay = remaining < PollInterval ? remaining : PollInterval;
            await Task.Delay(delay, ctx.CancellationToken).ConfigureAwait(false);
        }
    }

    private ConditionEvaluation EvaluateCondition(ExecutionContext ctx)
    {
        var resolved = ctx.Templates.Resolve(ConditionExpression, ctx.Variables);
        var match = ConditionRegex.Match(resolved);
        if (!match.Success)
        {
            return new ConditionEvaluation(
                IsTrue: false,
                ResolvedText: resolved,
                Error: $"Не удалось разобрать условие после подстановки переменных: '{resolved}'.");
        }

        var left = Unquote(match.Groups["left"].Value);
        var right = Unquote(match.Groups["right"].Value);
        var comparisonOperator = match.Groups["op"].Value;

        int comparison;
        if (TryParseNumber(left, out var leftNumber) &&
            TryParseNumber(right, out var rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
        }
        else
        {
            comparison = string.Compare(left, right, StringComparison.Ordinal);
        }

        var isTrue = comparisonOperator switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            _ => false,
        };

        return new ConditionEvaluation(isTrue, resolved, Error: null);
    }

    private static bool TryParseNumber(string value, out decimal number)
    {
        return decimal.TryParse(
            value,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private BlockResult CreateResult(
        bool success,
        string message,
        TimeSpan elapsed,
        Dictionary<string, object> output)
    {
        output["mode"] = Mode.ToString();
        output["waitedMs"] = elapsed.TotalMilliseconds;

        return new BlockResult
        {
            Success = success,
            Message = message,
            Duration = elapsed,
            Output = output,
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " с";
    }

    private readonly record struct ConditionEvaluation(
        bool IsTrue,
        string ResolvedText,
        string? Error);
}
