using Microsoft.Extensions.Logging;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Reports;
using TestBuilder.Core.Scenarios;

namespace TestBuilder.Runner;

/// <summary>
/// Прикладной слой CLI-раннера: загрузка сценария, запуск, экспорт отчёта.
/// </summary>
public sealed class RunnerApplication
{
    private readonly ScenarioService _scenarioService;
    private readonly TestRunner _runner;
    private readonly ExecutionContextFactory _contextFactory;
    private readonly IReadOnlyList<IReportExporter> _exporters;
    private readonly ILogger<RunnerApplication> _logger;

    /// <summary>
    /// Создаёт раннер.
    /// </summary>
    /// <param name="scenarioService">Сервис сценариев.</param>
    /// <param name="runner">Движок выполнения.</param>
    /// <param name="contextFactory">Фабрика контекста.</param>
    /// <param name="exporters">Доступные экспортёры отчётов.</param>
    /// <param name="logger">Логгер.</param>
    public RunnerApplication(
        ScenarioService scenarioService,
        TestRunner runner,
        ExecutionContextFactory contextFactory,
        IEnumerable<IReportExporter> exporters,
        ILogger<RunnerApplication> logger)
    {
        _scenarioService = scenarioService ?? throw new ArgumentNullException(nameof(scenarioService));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _exporters = (exporters ?? throw new ArgumentNullException(nameof(exporters))).ToArray();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Выполняет сценарий и экспортирует отчёт.
    /// </summary>
    /// <param name="scenarioPath">Путь к файлу <c>.tbtest</c>.</param>
    /// <param name="outputPath">Путь к файлу отчёта.</param>
    /// <param name="format">Формат отчёта: <c>junit</c>, <c>json</c>, <c>html</c>.</param>
    /// <param name="environment">Необязательное имя окружения (зарезервировано для будущего).</param>
    /// <param name="verbose">Подробный вывод шагов.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>0 — все шаги успешны; 1 — есть ошибки; 2 — ошибка запуска/конфигурации.</returns>
    public async Task<int> RunAsync(
        string scenarioPath,
        string outputPath,
        string format,
        string? environment = null,
        bool verbose = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        var exporter = _exporters.FirstOrDefault(item =>
            string.Equals(item.Format, format, StringComparison.OrdinalIgnoreCase));

        if (exporter is null)
        {
            var available = string.Join(", ", _exporters.Select(item => item.Format));
            Console.Error.WriteLine(
                $"Неизвестный формат отчёта '{format}'. Доступны: {available}.");
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(environment))
        {
            _logger.LogDebug(
                "Параметр --environment '{Environment}' принят, но выбор окружения пока не реализован.",
                environment);
        }

        try
        {
            var scenario = _scenarioService.Load(scenarioPath);
            var context = await _contextFactory.CreateAsync(scenario, ct).ConfigureAwait(false);
            var progress = new ConsoleStepProgress(verbose);

            Console.WriteLine($"Сценарий: {scenario.Name}");
            Console.WriteLine($"Шагов: {scenario.Blocks.Count}");

            var report = await _runner
                .RunAsync(scenario, context, progress)
                .ConfigureAwait(false);

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = File.Create(outputPath))
            {
                exporter.Export(report, stream);
            }

            var failed = report.Steps.Count(step => step.Status is StepStatus.Failed or StepStatus.Cancelled);
            Console.WriteLine(
                $"Итог: {(report.AllPassed ? "PASSED" : "FAILED")}; " +
                $"шагов: {report.Steps.Count}; ошибок: {failed}; " +
                $"длительность: {report.TotalDuration.TotalSeconds:0.###} с.");
            Console.WriteLine($"Отчёт: {Path.GetFullPath(outputPath)}");

            _logger.LogInformation(
                "Сценарий '{Name}' завершён. AllPassed={AllPassed}. Отчёт: {Report}.",
                scenario.Name,
                report.AllPassed,
                outputPath);

            return report.AllPassed ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Выполнение отменено.");
            return 1;
        }
        catch (ValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка CLI-раннера.");
            Console.Error.WriteLine($"Ошибка выполнения: {ex.Message}");
            return 2;
        }
    }

    private sealed class ConsoleStepProgress : IProgress<StepProgress>
    {
        private readonly bool _verbose;

        public ConsoleStepProgress(bool verbose)
        {
            _verbose = verbose;
        }

        public void Report(StepProgress value)
        {
            switch (value.Status)
            {
                case StepStatus.Running:
                    if (_verbose)
                    {
                        Console.WriteLine($"→ {value.Title}...");
                    }

                    break;

                case StepStatus.Success:
                case StepStatus.Failed:
                case StepStatus.Cancelled:
                    var duration = value.Result is null
                        ? string.Empty
                        : $" ({value.Result.Duration.TotalSeconds:0.###} с)";
                    Console.WriteLine($"[{value.Status}] {value.Title}{duration}");
                    if (_verbose && value.Result is not null && !string.IsNullOrWhiteSpace(value.Result.Message))
                    {
                        Console.WriteLine($"  {value.Result.Message}");
                    }

                    break;
            }
        }
    }
}
