using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using TestBuilder.Blocks;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.DependencyInjection;
using TestBuilder.Core.Reports;

namespace TestBuilder.Runner;

internal static class Program
{
    /// <summary>
    /// Точка входа CLI-раннера.
    /// </summary>
    /// <param name="args">Аргументы командной строки.</param>
    /// <returns>Код выхода процесса.</returns>
    private static async Task<int> Main(string[] args)
    {
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.Console()
            .CreateLogger();

        var scenarioOption = new Option<string>(
            "--scenario",
            "Путь к файлу сценария .tbtest.")
        {
            IsRequired = true,
        };

        var outputOption = new Option<string>(
            "--output",
            "Путь к файлу отчёта.")
        {
            IsRequired = true,
        };

        var formatOption = new Option<string>(
            "--format",
            () => "junit",
            "Формат отчёта: junit, json или html.");

        var environmentOption = new Option<string?>(
            "--environment",
            "Имя окружения (зарезервировано для будущих версий).");

        var verboseOption = new Option<bool>(
            "--verbose",
            "Подробный вывод шагов.");

        var rootCommand = new RootCommand("Test Builder CLI — запуск .tbtest сценариев в CI.");
        rootCommand.AddOption(scenarioOption);
        rootCommand.AddOption(outputOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(environmentOption);
        rootCommand.AddOption(verboseOption);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));
        services.AddTestBuilderCore();
        services.AddSingleton<IReportExporter, JsonReportExporter>();
        services.AddSingleton<IReportExporter, HtmlReportExporter>();
        services.AddSingleton<IReportExporter, JUnitXmlReportExporter>();
        services.AddSingleton(provider =>
        {
            provider.GetRequiredService<BlockRegistry>().AddBuiltInBlocks();
            return ActivatorUtilities.CreateInstance<RunnerApplication>(provider);
        });

        using var provider = services.BuildServiceProvider();
        int? exitCode = null;

        rootCommand.SetHandler(
            async (string scenario, string output, string format, string? environment, bool verbose) =>
            {
                levelSwitch.MinimumLevel = verbose
                    ? LogEventLevel.Debug
                    : LogEventLevel.Information;

                var application = provider.GetRequiredService<RunnerApplication>();
                exitCode = await application.RunAsync(
                    scenario,
                    output,
                    format,
                    environment,
                    verbose);
            },
            scenarioOption,
            outputOption,
            formatOption,
            environmentOption,
            verboseOption);

        try
        {
            var parseExitCode = await rootCommand.InvokeAsync(args);
            return parseExitCode == 0 ? exitCode ?? 0 : parseExitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Не удалось выполнить CLI-раннер.");
            return 2;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
