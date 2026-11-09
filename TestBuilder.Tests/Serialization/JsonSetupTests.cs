using System.Text.Json;
using FluentAssertions;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using TestBuilder.Core.Scenarios;
using TestBuilder.Core.Serialization;
using TestBuilder.Tests.TestDoubles;
using Xunit;

namespace TestBuilder.Tests.Serialization;

/// <summary>
/// Проверки полиморфной сериализации сценариев.
/// </summary>
public sealed class JsonSetupTests
{
    [Fact]
    public void SerializeAndDeserialize_PreservesBlockTypeAndPayload()
    {
        // Arrange
        var registry = CreateRegistry();
        var options = JsonSetup.CreateDefault(registry);
        var scenario = new TestScenario
        {
            Name = "Полиморфный сценарий",
            Environment = new TestBuilder.Core.Scenarios.Environment
            {
                Name = "test",
                Variables = new Dictionary<string, string> { ["baseUrl"] = "https://example.test" },
            },
            Blocks =
            {
                new FakeBlock
                {
                    Title = "Проверяемый блок",
                    Delay = TimeSpan.FromSeconds(1.5),
                    Mode = FakeMode.Second,
                    Items = { new KeyValueItem { Key = "a", Value = "b" } },
                },
            },
        };

        // Act
        var json = JsonSerializer.Serialize(scenario, options);
        var loaded = JsonSerializer.Deserialize<TestScenario>(json, options);

        // Assert
        json.Should().Contain("\"$type\": \"fake\"");
        json.Should().NotContain("\"blockType\"");
        json.Should().Contain("\"mode\": \"second\"");

        loaded.Should().NotBeNull();
        loaded!.Blocks.Should().ContainSingle();
        var block = loaded.Blocks[0].Should().BeOfType<FakeBlock>().Which;
        block.Title.Should().Be("Проверяемый блок");
        block.Delay.Should().Be(TimeSpan.FromSeconds(1.5));
        block.Mode.Should().Be(FakeMode.Second);
        block.Items.Should().ContainSingle().Which.Key.Should().Be("a");
    }

    [Fact]
    public void Deserialize_UnknownBlockType_ThrowsJsonException()
    {
        // Arrange
        var json = """
                   {
                     "name": "Неизвестный блок",
                     "blocks": [
                       { "$type": "unknown", "id": "8f62050d-8f20-4f77-9a98-dd17a0d7e0ea" }
                     ]
                   }
                   """;

        var options = JsonSetup.CreateDefault(new BlockRegistry());

        // Act
        Action action = () => JsonSerializer.Deserialize<TestScenario>(json, options);

        // Assert
        action.Should().Throw<JsonException>().WithMessage("*unknown*");
    }

    private static BlockRegistry CreateRegistry()
    {
        var registry = new BlockRegistry();
        registry.Register(new FakeBlockFactory());
        return registry;
    }
}
