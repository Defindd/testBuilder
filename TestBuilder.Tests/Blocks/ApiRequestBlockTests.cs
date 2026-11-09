using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using TestBuilder.Blocks.Api;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Scenarios;
using TestBuilder.Core.Templating;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;
using Xunit;

namespace TestBuilder.Tests.Blocks;

/// <summary>
/// Интеграционные тесты <see cref="ApiRequestBlock"/> на WireMock.Net.
/// </summary>
public sealed class ApiRequestBlockTests
{
    [Fact]
    public async Task Get_WithStatus200AndNoChecks_ReturnsSuccess()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/ok").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Url = BaseUrl(server) + "/ok",
            TimeoutSeconds = 10,
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient));

        result.Success.Should().BeTrue(result.Message);
        result.Output.Should().ContainKey("status");
        result.Output["status"].Should().Be(200);
    }

    [Fact]
    public async Task Get_WithStatus500AndNoChecks_ReturnsFailure()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/fail").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Url = BaseUrl(server) + "/fail",
            TimeoutSeconds = 10,
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("500");
        result.Output["status"].Should().Be(500);
    }

    [Fact]
    public async Task PostJson_WithJsonPathCheck_ReturnsSuccess()
    {
        const string requestBody = """{"name":"test"}""";
        const string responseBody = """{"id":42,"name":"test"}""";

        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/items")
                    .UsingPost()
                    .WithBody(requestBody))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(responseBody));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Method = HttpMethodType.Post,
            Url = BaseUrl(server) + "/items",
            BodyType = HttpBodyType.Json,
            Body = requestBody,
            TimeoutSeconds = 10,
            ResponseChecks =
            {
                new JsonPathCheck
                {
                    Path = "$.id",
                    Operator = CheckOperator.Equals,
                    ExpectedValue = "42",
                },
            },
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient));

        result.Success.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task ContainsCheck_WhenValueMissing_ReturnsFailure()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/data").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"name":"test"}"""));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Url = BaseUrl(server) + "/data",
            TimeoutSeconds = 10,
            ResponseChecks =
            {
                new JsonPathCheck
                {
                    Path = "$.name",
                    Operator = CheckOperator.Contains,
                    ExpectedValue = "missing",
                },
            },
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не пройдена");
    }

    [Fact]
    public async Task Url_WithBaseUrlTemplate_IsResolved()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/template").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(204));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Url = "{{baseUrl}}/template",
            TimeoutSeconds = 10,
        };
        var variables = new Dictionary<string, object> { ["baseUrl"] = BaseUrl(server) };

        var result = await block.ExecuteAsync(CreateContext(httpClient, variables));

        result.Success.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task SaveResponseVariable_IsAvailableToNextBlock()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/saved").UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"id":42}"""));

        using var httpClient = new HttpClient();
        var apiBlock = new ApiRequestBlock
        {
            Url = BaseUrl(server) + "/saved",
            TimeoutSeconds = 10,
            SaveResponseVariable = "lastResponse",
        };

        var scenario = new TestScenario
        {
            Name = "Save response",
            Blocks =
            {
                apiBlock,
                new AssertSavedResponseBlock(),
            },
        };

        var runner = new TestRunner(NullLogger<TestRunner>.Instance);

        var report = await runner.RunAsync(scenario, CreateContext(httpClient));

        report.AllPassed.Should().BeTrue();
        report.Steps.Should().HaveCount(2);
        report.Steps[1].Status.Should().Be(StepStatus.Success);
    }

    [Fact]
    public async Task BearerAuth_IsAppliedWithoutAuthorizationHeaderRow()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/auth")
                    .UsingGet()
                    .WithHeader("Authorization", "Bearer test-token"))
            .RespondWith(Response.Create().WithStatusCode(200));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Url = BaseUrl(server) + "/auth",
            AuthType = HttpAuthType.Bearer,
            AuthToken = "test-token",
            TimeoutSeconds = 10,
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient));

        result.Success.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task BearerAuth_NormalizesTokenWithBearerPrefix()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/auth-normalized")
                    .UsingGet()
                    .WithHeader("Authorization", "Bearer test-token"))
            .RespondWith(Response.Create().WithStatusCode(200));

        using var httpClient = new HttpClient();
        var block = new ApiRequestBlock
        {
            Url = BaseUrl(server) + "/auth-normalized",
            AuthType = HttpAuthType.Bearer,
            AuthToken = "  Bearer test-token  ",
            TimeoutSeconds = 10,
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient));

        result.Success.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task SaveRequestJson_IsAvailableForJsonFragmentsInNextBlock()
    {
        const string expectedBody = """{"copied":{"name":"Ann"},"name":"Ann"}""";

        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/save-request").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
        server.Given(
                Request.Create()
                    .WithPath("/use-request")
                    .UsingPost()
                    .WithBody(expectedBody))
            .RespondWith(Response.Create().WithStatusCode(200));

        using var httpClient = new HttpClient();
        var context = CreateContext(httpClient);

        var first = new ApiRequestBlock
        {
            Method = HttpMethodType.Post,
            Url = BaseUrl(server) + "/save-request",
            BodyType = HttpBodyType.Json,
            Body = """{"customer":{"name":"Ann"}}""",
            SaveRequestJson = true,
            SaveRequestJsonVariable = "requestJson",
            TimeoutSeconds = 10,
        };

        var firstResult = await first.ExecuteAsync(context);
        firstResult.Success.Should().BeTrue(firstResult.Message);
        context.Variables["requestJson"].Should().BeOfType<JObject>();

        var second = new ApiRequestBlock
        {
            Method = HttpMethodType.Post,
            Url = BaseUrl(server) + "/use-request",
            BodyType = HttpBodyType.Json,
            Body = """{"copied":{$requestJson.customer},"name":{$requestJson.customer.name}}""",
            TimeoutSeconds = 10,
        };

        var secondResult = await second.ExecuteAsync(context);
        secondResult.Success.Should().BeTrue(secondResult.Message);
    }

    private static string BaseUrl(WireMockServer server)
    {
        return (server.Url ?? throw new InvalidOperationException("WireMock server URL is not available."))
            .TrimEnd('/');
    }

    private static ExecutionContext CreateContext(
        HttpClient httpClient,
        Dictionary<string, object>? variables = null)
    {
        return new ExecutionContext
        {
            HttpClient = httpClient,
            Logger = NullLogger.Instance,
            CancellationToken = CancellationToken.None,
            Templates = new TemplateEngine(),
            Variables = variables ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
        };
    }

    private sealed class AssertSavedResponseBlock : ITestBlock
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "Проверить сохранённый ответ";
        public bool Enabled { get; set; } = true;
        public string BlockType => "assertSavedResponse";

        public Task<BlockResult> ExecuteAsync(ExecutionContext ctx)
        {
            if (!ctx.Variables.TryGetValue("lastResponse", out var value))
            {
                return Task.FromResult(BlockResult.Fail("Переменная lastResponse не найдена."));
            }

            if (value is not JToken token)
            {
                return Task.FromResult(
                    BlockResult.Fail($"Ожидался JToken, получено: {value?.GetType().FullName ?? "null"}."));
            }

            var id = token.SelectToken("$.id")?.Value<int>();
            return Task.FromResult(
                id == 42
                    ? BlockResult.Ok("Сохранённый ответ содержит id=42.")
                    : BlockResult.Fail($"Ожидался id=42, получено: {id}."));
        }
    }
}
