using FluentAssertions;
using TestBuilder.Blocks.Api;
using TestBuilder.Blocks.Wait;
using TestBuilder.Core.Scenarios;
using TestBuilder.UI.Json;
using Xunit;

namespace TestBuilder.Tests.UI;

/// <summary>
/// Проверки каталога JSON-переменных для автодополнения.
/// </summary>
public sealed class JsonVariableCatalogTests
{
    [Fact]
    public void Build_IncludesOnlyPrecedingSavedRequestJsonVariables()
    {
        var scenario = new TestScenario
        {
            Blocks =
            {
                new ApiRequestBlock
                {
                    Title = "Save request",
                    BodyType = HttpBodyType.Json,
                    Body = """{"id":"{{id}}","customer":{"name":"Ann"}}""",
                    SaveRequestJson = true,
                    SaveRequestJsonVariable = "requestJson",
                },
                new WaitBlock { Title = "Next" },
                new ApiRequestBlock
                {
                    Title = "Later",
                    BodyType = HttpBodyType.Json,
                    Body = """{"later":true}""",
                    SaveRequestJson = true,
                    SaveRequestJsonVariable = "laterJson",
                },
            },
        };

        var variables = JsonVariableCatalog.Build(scenario, beforeBlockIndex: 1);

        variables.Should().ContainSingle();
        variables[0].Name.Should().Be("requestJson");
        variables[0].Paths.Should().Contain("requestJson.customer");
        variables[0].Paths.Should().Contain("requestJson.customer.name");
        variables[0].Paths.Should().NotContain("laterJson");
    }
}
