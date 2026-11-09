using FluentAssertions;
using Newtonsoft.Json.Linq;
using TestBuilder.Core.Templating;
using TestBuilder.Tests.TestDoubles;
using Xunit;

namespace TestBuilder.Tests.Templating;

/// <summary>
/// Проверки подстановки переменных.
/// </summary>
public sealed class TemplateEngineTests
{
    [Fact]
    public void Resolve_ReplacesSimpleAndNestedVariables()
    {
        var engine = new TemplateEngine();
        var variables = new Dictionary<string, object>
        {
            ["name"] = "Test Builder",
            ["response"] = JObject.Parse("""{"status":200,"data":{"id":42}}"""),
        };

        var result = engine.Resolve(
            "{{name}}/{{response.status}}/{{response.data.id}}/{{missing}}",
            variables);

        result.Should().Be("Test Builder/200/42/");
    }

    [Fact]
    public void ResolveDeep_UpdatesNestedStringProperties()
    {
        var engine = new TemplateEngine();
        var block = new FakeBlock
        {
            Title = "{{name}}",
            Url = "{{response.data.id}}",
            Items = { new() { Key = "id", Value = "{{response.data.id}}" } },
        };

        engine.ResolveDeep(
            block,
            new Dictionary<string, object>
            {
                ["name"] = "Deep",
                ["response"] = JObject.Parse("""{"data":{"id":42}}"""),
            });

        block.Title.Should().Be("Deep");
        block.Url.Should().Be("42");
        block.Items[0].Value.Should().Be("42");
    }

    [Fact]
    public void ResolveJson_InsertsNestedJsonFragment()
    {
        var engine = new TemplateEngine();
        var variables = new Dictionary<string, object>
        {
            ["id"] = 42,
            ["name"] = "Ann",
            ["response"] = JObject.Parse(
                """{"customer":{"id":1,"name":"Ann"},"tags":["a","b"]}"""),
        };

        var json = engine.ResolveJson(
            """{"customer":{$response.customer},"name":"{{name}}","id":{{id}},"firstTag":{$response.tags.0}}""",
            variables);

        var parsed = JObject.Parse(json);
        parsed["customer"]!["id"]!.Value<int>().Should().Be(1);
        parsed["name"]!.Value<string>().Should().Be("Ann");
        parsed["id"]!.Value<int>().Should().Be(42);
        parsed["firstTag"]!.Value<string>().Should().Be("a");
    }

    [Fact]
    public void ResolveJson_UnknownFragment_InsertsNull()
    {
        var engine = new TemplateEngine();

        var json = engine.ResolveJson("""{"value":{$missing.path}}""", new Dictionary<string, object>());

        JObject.Parse(json)["value"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void Resolve_RandomUuid_ReturnsValidGuid()
    {
        var engine = new TemplateEngine();

        var result = engine.Resolve("{{random.uuid}}", new Dictionary<string, object>());

        Guid.TryParse(result, out var guid).Should().BeTrue();
        guid.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Resolve_RandomNumber_RespectsRange()
    {
        var engine = new TemplateEngine();

        engine.Resolve("{{random.number:10:10}}", new Dictionary<string, object>())
            .Should().Be("10");

        var defaultNumber = int.Parse(engine.Resolve(
            "{{random.number}}",
            new Dictionary<string, object>()));

        defaultNumber.Should().BeGreaterThanOrEqualTo(1);
        defaultNumber.Should().BeLessThanOrEqualTo(1_000_000);
    }
}
