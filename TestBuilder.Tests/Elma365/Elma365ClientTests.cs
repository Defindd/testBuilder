using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TestBuilder.Elma365;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TestBuilder.Tests.Elma365;

/// <summary>
/// РўРµСЃС‚С‹ РєР»РёРµРЅС‚Р° Elma365 РЅР° WireMock.
/// </summary>
public sealed class Elma365ClientTests
{
    [Fact]
    public async Task GetObjectAsync_ReturnsObject()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/42/get")
                    .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"id":42,"status":"active"}"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        var result = await client.GetObjectAsync("orders", "order", "42", CancellationToken.None);

        result.Value<int>("id").Should().Be(42);
        result.Value<string>("status").Should().Be("active");
    }

    [Fact]
    public async Task GetObjectTypesAsync_ReturnsArray()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/objects")
                    .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""[{"code":"order"},{"code":"customer"}]"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        var result = await client.GetObjectTypesAsync("orders", CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetObjectSchemaAsync_ReturnsObject()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/schema")
                    .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"fields":[{"code":"status"}]}"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        var result = await client.GetObjectSchemaAsync("orders", "order", CancellationToken.None);

        result["fields"].Should().NotBeNull();
    }

    [Fact]
    public async Task GetObjectAsync_When404_ThrowsNotFoundException()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/pub/v1/app/orders/order/404/get").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(404).WithBody("not found"));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        Func<Task> action = () => client.GetObjectAsync("orders", "order", "404", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<Elma365NotFoundException>();
        exception.Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetObjectAsync_When401_ThrowsAuthException()
    {
        using var server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/pub/v1/app/orders/order/42/get").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401).WithBody("unauthorized"));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient, token: "invalid-token");

        Func<Task> action = () => client.GetObjectAsync("orders", "order", "42", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<Elma365AuthException>();
        exception.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetObjectAsync_When503Then200_RetriesRequest()
    {
        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/retry/get")
                    .UsingGet())
            .InScenario("retry")
            .WillSetStateTo("second")
            .RespondWith(Response.Create().WithStatusCode(503));

        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/retry/get")
                    .UsingGet())
            .InScenario("retry")
            .WhenStateIs("second")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"id":"retry","status":"active"}"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        var result = await client.GetObjectAsync("orders", "order", "retry", CancellationToken.None);

        result.Value<string>("id").Should().Be("retry");
    }

    [Fact]
    public async Task FindObjectAsync_ByFilter_UsesPostListAndReturnsFirstObject()
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
                    .WithBody("""[{"id":"first","status":"active"},{"id":"second","status":"active"}]"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        var result = await client.FindObjectAsync("orders", "order", filter, CancellationToken.None);

        result.Value<string>("id").Should().Be("first");
    }

    [Fact]
    public async Task FindObjectAsync_ByFilter_ReadsNestedResultAndTotal()
    {
        const string filter = """{"filter":{"tf":{"status":"active"}}}""";

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
                        """{"success":true,"result":{"result":[{"id":"nested","status":"active"}],"total":1}}"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        var result = await client.FindObjectAsync("orders", "order", filter, CancellationToken.None);

        result.Value<string>("id").Should().Be("nested");
    }

    [Fact]
    public async Task FindObjectAsync_WhenResultTotalZero_ThrowsNotFound()
    {
        const string filter = """{"filter":{"tf":{"status":"missing"}}}""";

        using var server = WireMockServer.Start();
        server.Given(
                Request.Create()
                    .WithPath("/pub/v1/app/orders/order/list")
                    .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(
                        """{"success":true,"result":{"result":[],"total":0}}"""));

        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        Func<Task> action = () => client.FindObjectAsync("orders", "order", filter, CancellationToken.None);

        await action.Should().ThrowAsync<Elma365NotFoundException>();
    }

    [Fact]
    public async Task FindObjectAsync_WithInvalidJson_ThrowsClearException()
    {
        using var server = WireMockServer.Start();
        using var httpClient = new HttpClient();
        var client = CreateClient(server, httpClient);

        Func<Task> action = () => client.FindObjectAsync(
            "orders",
            "order",
            """{"filter":{"tf":{"package":["{$request.package_id}}"}}""",
            CancellationToken.None);

        var exception = await action.Should().ThrowAsync<Elma365Exception>();
        exception.Which.Message.Should().Contain("не является корректным JSON");
    }

    private static Elma365Client CreateClient(
        WireMockServer server,
        HttpClient httpClient,
        string token = "test-token")
    {
        return new Elma365Client(
            httpClient,
            BaseUrl(server),
            token,
            NullLogger<Elma365Client>.Instance);
    }

    private static string BaseUrl(WireMockServer server)
    {
        return (server.Url ?? throw new InvalidOperationException("WireMock server URL is not available."))
            .TrimEnd('/');
    }
}

