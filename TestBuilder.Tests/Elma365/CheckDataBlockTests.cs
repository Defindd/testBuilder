using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using TestBuilder.Blocks;
using TestBuilder.Blocks.Elma365;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Checks;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Serialization;
using TestBuilder.Core.Templating;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;
using Xunit;

namespace TestBuilder.Tests.Elma365;

/// <summary>
/// Тесты <see cref="CheckDataBlock"/> на WireMock.
/// </summary>
public sealed class CheckDataBlockTests
{
    [Fact]
    public async Task Success_WithFieldChecks_SavesObjectToVariable()
    {
        using var server = WireMockServer.Start();
        MockObject(server, "42", """{"id":"42","status":"active","amount":100}""", 200);

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = "{{baseUrl}}",
            AppNamespace = "orders",
            AppCode = "order",
            ObjectId = "{{orderId}}",
            AuthTokenVariable = "elmaToken",
            FieldChecks =
            {
                new JsonPathCheck
                {
                    Path = "$.status",
                    Operator = CheckOperator.Equals,
                    ExpectedValue = "active",
                },
                new JsonPathCheck
                {
                    Path = "$.amount",
                    Operator = CheckOperator.Greater,
                    ExpectedValue = "50",
                },
            },
            SaveObjectVariable = "loadedOrder",
        };

        var variables = new Dictionary<string, object>
        {
            ["baseUrl"] = BaseUrl(server),
            ["orderId"] = "42",
            ["elmaToken"] = "test-token",
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, variables));

        result.Success.Should().BeTrue(result.Message);
        variables["loadedOrder"].Should().BeOfType<JObject>();
        ((JObject)variables["loadedOrder"])["status"]!.Value<string>().Should().Be("active");
    }

    [Fact]
    public async Task WhenObjectNotFound_ReturnsFailure()
    {
        using var server = WireMockServer.Start();
        MockObject(server, "404", "not found", 404);

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = BaseUrl(server),
            AppNamespace = "orders",
            AppCode = "order",
            ObjectId = "404",
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, CreateVariables()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не найден");
        result.Output["statusCode"].Should().Be(404);
    }

    [Fact]
    public async Task WhenTokenInvalid_ReturnsAuthorizationFailure()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/pub/v1/app/orders/order/42/get").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("unauthorized"));

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = BaseUrl(server),
            AppNamespace = "orders",
            AppCode = "order",
            ObjectId = "42",
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, CreateVariables()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("авторизации");
        result.Output["statusCode"].Should().Be(401);
    }

    [Fact]
    public async Task WhenFieldCheckFails_ReturnsFailure()
    {
        using var server = WireMockServer.Start();
        MockObject(server, "42", """{"id":"42","status":"active"}""", 200);

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = BaseUrl(server),
            AppNamespace = "orders",
            AppCode = "order",
            ObjectId = "42",
            FieldChecks =
            {
                new JsonPathCheck
                {
                    Path = "$.status",
                    Operator = CheckOperator.Equals,
                    ExpectedValue = "closed",
                },
            },
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, CreateVariables()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не пройдены");
        result.Message.Should().Contain("не пройдена");
    }

    [Fact]
    public async Task WhenTokenVariableMissing_ReturnsFailureWithoutHttpCall()
    {
        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = "https://example.test",
            AppNamespace = "orders",
            AppCode = "order",
            ObjectId = "42",
        };

        var result = await block.ExecuteAsync(
            CreateContext(httpClient, new Dictionary<string, object>()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("elmaToken");
    }

    [Fact]
    public async Task ByFilter_PostsJsonFilterAndUsesFirstFoundObject()
    {
        const string filter = """{"status":"active"}""";

        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/list")
                    .UsingPost()
                    .WithBody(filter))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"id":"42","status":"active"},{"id":"43","status":"active"}]"""));

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = BaseUrl(server),
            AppNamespace = "orders",
            AppCode = "order",
            SearchMode = CheckDataSearchMode.ByFilter,
            Filter = filter,
            FieldChecks =
            {
                new JsonPathCheck
                {
                    Path = "$.status",
                    Operator = CheckOperator.Equals,
                    ExpectedValue = "active",
                },
            },
            SaveObjectVariable = "filteredOrder",
        };
        var variables = CreateVariables();

        var result = await block.ExecuteAsync(CreateContext(httpClient, variables));

        result.Success.Should().BeTrue(result.Message);
        variables["filteredOrder"].Should().BeOfType<JObject>();
        ((JObject)variables["filteredOrder"]).Value<string>("id").Should().Be("42");
    }

    [Fact]
    public void LegacyObjectType_IsMappedToAppCodeAndNotWrittenBack()
    {
        var registry = new BlockRegistry();
        registry.AddBuiltInBlocks();
        var options = JsonSetup.CreateDefault(registry);
        var json = """
                   {
                     "$type": "checkData",
                     "id": "44444444-4444-4444-8444-444444444444",
                     "title": "Legacy",
                     "enabled": true,
                     "baseUrl": "https://example.test",
                     "authTokenVariable": "elmaToken",
                     "appNamespace": "orders",
                     "objectType": "legacy-order",
                     "objectId": "42"
                   }
                   """;

        var block = JsonSerializer.Deserialize<ITestBlock>(json, options)
            .Should().BeOfType<CheckDataBlock>().Which;

        block.AppCode.Should().Be("legacy-order");

        var serialized = JsonSerializer.Serialize(block, typeof(ITestBlock), options);
        serialized.Should().Contain("\"appCode\": \"legacy-order\"");
        serialized.Should().NotContain("\"objectType\"");
    }

    [Fact]
    public async Task DirectAuthToken_IsUsedWithoutVariable()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/42/get")
                    .UsingGet()
                    .WithHeader("Authorization", "Bearer direct-token"))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"id":"42","status":"active"}"""));

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = BaseUrl(server),
            AppNamespace = "orders",
            AppCode = "order",
            AuthToken = "Bearer direct-token",
            AuthTokenVariable = string.Empty,
            ObjectId = "42",
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, CreateVariables()));

        result.Success.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task ByFilter_WhenResultTotalIsZero_ReturnsFailure()
    {
        const string filter = """{"filter":{"tf":{"status":"missing"}}}""";

        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/list")
                    .UsingPost()
                    .WithBody(filter))
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(
                        """{"success":true,"result":{"result":[],"total":0}}"""));

        using var httpClient = new HttpClient();
        var block = new CheckDataBlock
        {
            BaseUrl = BaseUrl(server),
            AppNamespace = "orders",
            AppCode = "order",
            SearchMode = CheckDataSearchMode.ByFilter,
            Filter = filter,
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, CreateVariables()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("не найден");
    }

    private static void MockObject(
        WireMockServer server,
        string objectId,
        string body,
        int statusCode)
    {
        server.Given(
                Request.Create()
                    .WithPath($"/pub/v1/app/orders/order/{objectId}/get")
                    .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(statusCode)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(body));
    }

    private static Dictionary<string, object> CreateVariables()
    {
        return new Dictionary<string, object>
        {
            ["elmaToken"] = "test-token",
        };
    }

    private static ExecutionContext CreateContext(
        HttpClient httpClient,
        Dictionary<string, object> variables)
    {
        return new ExecutionContext
        {
            HttpClient = httpClient,
            Logger = NullLogger.Instance,
            CancellationToken = CancellationToken.None,
            Templates = new TemplateEngine(),
            Variables = variables,
        };
    }

    private static string BaseUrl(WireMockServer server)
    {
        return (server.Url ?? throw new InvalidOperationException("WireMock server URL is not available."))
            .TrimEnd('/');
    }
}
