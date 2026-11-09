using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestBuilder.Blocks;
using TestBuilder.Blocks.Wait;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.DependencyInjection;
using TestBuilder.Core.Reports;
using TestBuilder.Core.Scenarios;
using TestBuilder.Runner;
using Xunit;

namespace TestBuilder.Tests.Runner;

/// <summary>
/// Тесты CLI-приложения раннера.
/// </summary>
public sealed class RunnerApplicationTests
{
    [Fact]
    public async Task RunAsync_WhenAllStepsPass_ReturnsZeroAndWritesJson()
    {
        using var provider = CreateProvider();
        var application = CreateApplication(provider);
        var scenarioPath = Path.Combine(AppContext.BaseDirectory, $"runner-ok-{Guid.NewGuid():N}.tbtest");
        var outputPath = Path.Combine(AppContext.BaseDirectory, $"runner-ok-{Guid.NewGuid():N}.json");

        try
        {
            var scenario = new TestScenario
            {
                Name = "Runner success",
                Blocks =
                {
                    new WaitBlock
                    {
                        Title = "Короткое ожидание",
                        Duration = TimeSpan.FromMilliseconds(10),
                    },
                },
            };

            provider.GetRequiredService<ScenarioService>().Save(scenario, scenarioPath);

            var exitCode = await application.RunAsync(scenarioPath, outputPath, "json");

            exitCode.Should().Be(0);
            File.Exists(outputPath).Should().BeTrue();

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            document.RootElement.GetProperty("allPassed").GetBoolean().Should().BeTrue();
            document.RootElement.GetProperty("steps").GetArrayLength().Should().Be(1);
        }
        finally
        {
            DeleteIfExists(scenarioPath);
            DeleteIfExists(outputPath);
        }
    }

    [Fact]
    public async Task RunAsync_WhenStepFails_ReturnsOneAndWritesJUnitFailure()
    {
        using var provider = CreateProvider();
        var application = CreateApplication(provider);
        var scenarioPath = Path.Combine(AppContext.BaseDirectory, $"runner-fail-{Guid.NewGuid():N}.tbtest");
        var outputPath = Path.Combine(AppContext.BaseDirectory, $"runner-fail-{Guid.NewGuid():N}.xml");

        try
        {
            var scenario = new TestScenario
            {
                Name = "Runner failure",
                Variables = new Dictionary<string, string> { ["x"] = "0" },
                Blocks =
                {
                    new WaitBlock
                    {
                        Title = "Невыполнимое условие",
                        Mode = WaitMode.UntilCondition,
                        ConditionExpression = "{{x}} == 1",
                        MaxWait = TimeSpan.FromMilliseconds(100),
                        PollInterval = TimeSpan.FromMilliseconds(10),
                    },
                },
            };

            provider.GetRequiredService<ScenarioService>().Save(scenario, scenarioPath);

            var exitCode = await application.RunAsync(scenarioPath, outputPath, "junit");

            exitCode.Should().Be(1);
            File.Exists(outputPath).Should().BeTrue();

            var xml = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(outputPath));
            xml.Should().Contain("<testsuites");
            xml.Should().Contain("<testcase");
            xml.Should().Contain("<failure");
        }
        finally
        {
            DeleteIfExists(scenarioPath);
            DeleteIfExists(outputPath);
        }
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTestBuilderCore();
        services.AddSingleton<IReportExporter, JsonReportExporter>();
        services.AddSingleton<IReportExporter, HtmlReportExporter>();
        services.AddSingleton<IReportExporter, JUnitXmlReportExporter>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<BlockRegistry>().AddBuiltInBlocks();
        return provider;
    }

    private static RunnerApplication CreateApplication(ServiceProvider provider)
    {
        return ActivatorUtilities.CreateInstance<RunnerApplication>(provider);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
