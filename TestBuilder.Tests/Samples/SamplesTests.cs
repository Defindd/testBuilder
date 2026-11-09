using FluentAssertions;
using TestBuilder.Blocks;
using TestBuilder.Blocks.Api;
using TestBuilder.Blocks.Elma365;
using TestBuilder.Blocks.Wait;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Scenarios;
using Xunit;

namespace TestBuilder.Tests.Samples;

/// <summary>
/// Проверки загрузки и пересохранения sample-сценариев.
/// </summary>
public sealed class SamplesTests
{
    [Fact]
    public void SimpleHttpSample_LoadsAndRoundTrips()
    {
        var scenario = LoadSample("sample-simple-http.tbtest");

        scenario.Blocks.Should().ContainSingle();
        scenario.Blocks[0].Should().BeOfType<ApiRequestBlock>();
        RoundTrip(scenario).Blocks.Should().ContainSingle();
    }

    [Fact]
    public void WithWaitSample_LoadsThreeBlocksAndRoundTrips()
    {
        var scenario = LoadSample("sample-with-wait.tbtest");

        scenario.Blocks.Should().HaveCount(3);
        scenario.Blocks[0].Should().BeOfType<ApiRequestBlock>();
        scenario.Blocks[1].Should().BeOfType<WaitBlock>();
        scenario.Blocks[2].Should().BeOfType<ApiRequestBlock>();

        var reloaded = RoundTrip(scenario);
        reloaded.Blocks.Should().HaveCount(3);
        reloaded.Blocks[1].Should().BeOfType<WaitBlock>()
            .Which.Mode.Should().Be(WaitMode.Fixed);
    }

    [Fact]
    public void Elma365Sample_LoadsCheckDataBlockAndRoundTrips()
    {
        var scenario = LoadSample("sample-elma365.tbtest");

        scenario.Blocks.Should().ContainSingle();
        scenario.Blocks[0].Should().BeOfType<CheckDataBlock>();
        RoundTrip(scenario).Blocks[0].Should().BeOfType<CheckDataBlock>();
    }

    [Fact]
    public void JsonVariableSample_LoadsAndRoundTrips()
    {
        var scenario = LoadSample("sample-json-variable.tbtest");

        scenario.Blocks.Should().HaveCount(2);
        scenario.Blocks[0].Should().BeOfType<ApiRequestBlock>()
            .Which.SaveRequestJson.Should().BeTrue();
        scenario.Blocks[0].Should().BeOfType<ApiRequestBlock>()
            .Which.SaveRequestJsonVariable.Should().Be("requestJson");
        scenario.Blocks[1].Should().BeOfType<ApiRequestBlock>()
            .Which.Body.Should().Contain("{$requestJson.customer}");

        RoundTrip(scenario).Blocks.Should().HaveCount(2);
    }

    private static TestScenario LoadSample(string fileName)
    {
        var registry = new BlockRegistry();
        registry.AddBuiltInBlocks();
        var service = new ScenarioService(registry);
        var path = Path.Combine(FindRepositoryRoot(), "samples", fileName);

        return service.Load(path);
    }

    private static TestScenario RoundTrip(TestScenario scenario)
    {
        var registry = new BlockRegistry();
        registry.AddBuiltInBlocks();
        var service = new ScenarioService(registry);
        var path = Path.Combine(AppContext.BaseDirectory, $"sample-{Guid.NewGuid():N}.tbtest");

        try
        {
            service.Save(scenario, path);
            return service.Load(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TestBuilder.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найден корень репозитория TestBuilder.");
    }
}
