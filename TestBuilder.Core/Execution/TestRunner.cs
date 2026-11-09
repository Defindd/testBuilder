using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Scenarios;

namespace TestBuilder.Core.Execution;

/// <summary>
/// Последовательный движок выполнения тестового сценария.
/// </summary>
public sealed class TestRunner
{
    private readonly ILogger<TestRunner> _logger;

    /// <summary>
    /// Создаёт движок.
    /// </summary>
    /// <param name="logger">Логгер движка.</param>
    public TestRunner(ILogger<TestRunner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет включённые блоки сценария последовательно.
    /// Выполнение останавливается на первом неуспешном шаге, исключении или отмене.
    /// </summary>
    /// <param name="scenario">Сценарий для выполнения.</param>
    /// <param name="ctx">Контекст выполнения.</param>
    /// <param name="progress">Необязательный приёмник прогресса.</param>
    /// <returns>Отчёт о выполнении.</returns>
    public async Task<RunReport> RunAsync(
        TestScenario scenario,
        ExecutionContext ctx,
        IProgress<StepProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(ctx);

        var startedAt = DateTime.UtcNow;
        var totalStopwatch = Stopwatch.StartNew();
        var steps = new List<StepResult>();

        _logger.LogInformation("Запуск сценария '{ScenarioName}'.", scenario.Name);

        foreach (var block in scenario.Blocks.Where(block => block.Enabled))
        {
            if (ctx.CancellationToken.IsCancellationRequested)
            {
                const string message = "Выполнение отменено до запуска шага.";

                progress?.Report(new StepProgress(block.Id, block.Title, StepStatus.Cancelled, null));
                steps.Add(new StepResult
                {
                    BlockId = block.Id,
                    Title = block.Title,
                    BlockType = block.BlockType,
                    Status = StepStatus.Cancelled,
                    Message = message,
                });

                _logger.LogWarning("Сценарий остановлен: {Message}", message);
                break;
            }

            progress?.Report(new StepProgress(block.Id, block.Title, StepStatus.Running, null));
            _logger.LogDebug("Выполнение шага '{Title}' ({BlockType}).", block.Title, block.BlockType);

            var stepStopwatch = Stopwatch.StartNew();
            try
            {
                var result = await block.ExecuteAsync(ctx).ConfigureAwait(false)
                             ?? throw new InvalidOperationException(
                                 $"Блок '{block.Title}' ({block.BlockType}) вернул null вместо результата.");

                stepStopwatch.Stop();

                if (!result.Success)
                {
                    steps.Add(CreateStepResult(block, StepStatus.Failed, stepStopwatch.Elapsed, result.Message, result.Output));
                    progress?.Report(new StepProgress(block.Id, block.Title, StepStatus.Failed, result));

                    _logger.LogWarning(
                        "Шаг '{Title}' завершился ошибкой за {Duration}: {Message}",
                        block.Title,
                        stepStopwatch.Elapsed,
                        result.Message);
                    break;
                }

                steps.Add(CreateStepResult(block, StepStatus.Success, stepStopwatch.Elapsed, result.Message, result.Output));
                progress?.Report(new StepProgress(block.Id, block.Title, StepStatus.Success, result));

                _logger.LogInformation(
                    "Шаг '{Title}' выполнен успешно за {Duration}.",
                    block.Title,
                    stepStopwatch.Elapsed);
            }
            catch (OperationCanceledException ex)
            {
                stepStopwatch.Stop();
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Выполнение отменено."
                    : $"Выполнение отменено: {ex.Message}";

                steps.Add(CreateStepResult(block, StepStatus.Cancelled, stepStopwatch.Elapsed, message, null));
                progress?.Report(new StepProgress(block.Id, block.Title, StepStatus.Cancelled, BlockResult.Fail(message)));

                _logger.LogWarning("Шаг '{Title}' отменён: {Message}", block.Title, message);
                break;
            }
            catch (Exception ex)
            {
                stepStopwatch.Stop();
                var message = $"Исключение при выполнении шага: {ex.Message}";

                steps.Add(CreateStepResult(block, StepStatus.Failed, stepStopwatch.Elapsed, message, null));
                progress?.Report(new StepProgress(block.Id, block.Title, StepStatus.Failed, BlockResult.Fail(message)));

                _logger.LogError(ex, "Шаг '{Title}' завершился исключением.", block.Title);
                break;
            }
        }

        totalStopwatch.Stop();

        var report = new RunReport
        {
            ScenarioName = scenario.Name,
            StartedAt = startedAt,
            TotalDuration = totalStopwatch.Elapsed,
            Steps = steps,
        };

        _logger.LogInformation(
            "Сценарий '{ScenarioName}' завершён за {Duration}. Успех: {AllPassed}.",
            report.ScenarioName,
            report.TotalDuration,
            report.AllPassed);

        return report;
    }

    private static StepResult CreateStepResult(
        ITestBlock block,
        StepStatus status,
        TimeSpan duration,
        string message,
        Dictionary<string, object>? output)
    {
        return new StepResult
        {
            BlockId = block.Id,
            Title = block.Title,
            BlockType = block.BlockType,
            Status = status,
            Duration = duration,
            Message = message,
            OutputJson = SerializeOutput(output),
        };
    }

    private static string? SerializeOutput(Dictionary<string, object>? output)
    {
        if (output is null || output.Count == 0)
        {
            return null;
        }

        try
        {
            return JsonConvert.SerializeObject(output, Formatting.None);
        }
        catch (JsonException)
        {
            // Выходные данные не должны ломать отчёт: сохраняем хотя бы ключи.
            return JsonConvert.SerializeObject(output.Keys, Formatting.None);
        }
    }
}
