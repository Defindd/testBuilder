using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using TestBuilder.Blocks;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.DependencyInjection;
using TestBuilder.Core.Reports;

namespace TestBuilder.UI;

internal static class Program
{
    /// <summary>
    /// Главная точка входа приложения.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TestBuilder",
            "logs");
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));
            services.AddTestBuilderCore();
            services.AddSingleton<IReportExporter, JsonReportExporter>();
            services.AddSingleton<IReportExporter, HtmlReportExporter>();
            services.AddSingleton<IReportExporter, JUnitXmlReportExporter>();
            services.AddSingleton<MainForm>(provider =>
            {
                provider.GetRequiredService<BlockRegistry>().AddBuiltInBlocks();
                return ActivatorUtilities.CreateInstance<MainForm>(provider);
            });

            using var provider = services.BuildServiceProvider();
            var form = provider.GetRequiredService<MainForm>();

            Log.Information("Запуск Test Builder UI.");
            Application.Run(form);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Не удалось запустить Test Builder.");
            MessageBox.Show(
                $"Не удалось запустить Test Builder:{Environment.NewLine}{ex.Message}",
                "Test Builder",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
