using FluentAssertions;
using TestBuilder.Blocks.Api;
using TestBuilder.Blocks.Elma365;
using TestBuilder.Blocks.Wait;
using TestBuilder.Core.Checks;
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
    public void Build_IncludesApiResponseAndElmaObjectVariablesFromChecks()
    {
        var scenario = new TestScenario
        {
            Blocks =
            {
                new ApiRequestBlock
                {
                    SaveResponseVariable = "apiResponse",
                    ResponseChecks =
                    {
                        new JsonPathCheck { Path = "$.json.id" },
                        new JsonPathCheck { Path = "$.json.customer.name" },
                    },
                },
                new CheckDataBlock
                {
                    SaveObjectVariable = "elmaObject",
                    FieldChecks =
                    {
                        new JsonPathCheck { Path = "$.status" },
                    },
                },
                new WaitBlock(),
            },
        };

        var variables = JsonVariableCatalog.Build(scenario, beforeBlockIndex: 2);

        variables.Should().HaveCount(2);

        var apiResponse = variables.Single(variable => variable.Name == "apiResponse");
        apiResponse.Paths.Should().Contain("apiResponse.json.id");
        apiResponse.Paths.Should().Contain("apiResponse.json.customer.name");

        var elmaObject = variables.Single(variable => variable.Name == "elmaObject");
        elmaObject.Paths.Should().Contain("elmaObject.status");
    }

    [Fact]
    public void Build_IncludesBooleanStringAndTemplateFields()
    {
        var scenario = new TestScenario
        {
            Blocks =
            {
                new ApiRequestBlock
                {
                    BodyType = HttpBodyType.Json,
                    Body = """{"field1":"{{random.uuid}}","field2":true,"field3":"qwerty"}""",
                    SaveRequestJson = true,
                    SaveRequestJsonVariable = "requestJson",
                },
                new WaitBlock(),
            },
        };

        var paths = JsonVariableCatalog.Build(scenario, beforeBlockIndex: 1)[0].Paths;

        paths.Should().Contain("requestJson.field1");
        paths.Should().Contain("requestJson.field2");
        paths.Should().Contain("requestJson.field3");
    }

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
