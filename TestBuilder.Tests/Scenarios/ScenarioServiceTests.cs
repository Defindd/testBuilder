using FluentAssertions;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Scenarios;
using TestBuilder.Tests.TestDoubles;
using Xunit;

namespace TestBuilder.Tests.Scenarios;

/// <summary>
/// Проверки загрузки, сохранения и валидации сценариев.
/// </summary>
public sealed class ScenarioServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsPolymorphicBlockWithoutLosses()
    {
        var registry = CreateRegistry();
        var service = new ScenarioService(registry);
        var path = Path.Combine(AppContext.BaseDirectory, $"test-builder-{Guid.NewGuid():N}.tbtest");

        try
        {
            var scenario = new TestScenario
            {
                Name = "Roundtrip",
                Blocks =
                {
                    new FakeBlock
                    {
                        Title = "Шаг сохранения",
                        Delay = TimeSpan.FromMilliseconds(250),
                        Mode = FakeMode.Second,
                    },
                },
            };

            service.Save(scenario, path);
            var loaded = service.Load(path);

            loaded.Name.Should().Be("Roundtrip");
            loaded.Blocks.Should().ContainSingle();
            var block = loaded.Blocks[0].Should().BeOfType<FakeBlock>().Which;
            block.Title.Should().Be("Шаг сохранения");
            block.Delay.Should().Be(TimeSpan.FromMilliseconds(250));
            block.Mode.Should().Be(FakeMode.Second);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Validate_GeneratesTitlesAndReportsBlockErrors()
    {
        var registry = CreateRegistry();
        var service = new ScenarioService(registry);
        var duplicateId = Guid.NewGuid();
        var scenario = new TestScenario
        {
            Blocks =
            {
                new FakeBlock { Id = duplicateId, Title = string.Empty, RequireUrl = true },
                new FakeBlock { Id = duplicateId, Title = string.Empty, RequireUrl = true },
            },
        };

        Action action = () => service.Validate(scenario);

        var exception = action.Should().Throw<ValidationException>().Which;
        scenario.Blocks[0].Title.Should().Be("Шаг 1");
        scenario.Blocks[1].Title.Should().Be("Шаг 2");
        exception.Errors.Should().Contain(error => error.Contains("Url обязателен"));
        exception.Errors.Should().Contain(error => error.Contains("повторяется"));
    }

    private static BlockRegistry CreateRegistry()
    {
        var registry = new BlockRegistry();
        registry.Register(new FakeBlockFactory());
        return registry;
    }
}
