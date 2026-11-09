using System.Reflection;
using FluentAssertions;
using TestBuilder.UI;
using Xunit;

namespace TestBuilder.Tests.UI;

/// <summary>
/// Проверки форматирования раскрываемых деталей ошибки в логе.
/// </summary>
public sealed class MainFormLogDetailsTests
{
    [Fact]
    public void BuildLogDetails_ContainsRequestAndResponse()
    {
        var method = typeof(MainForm).GetMethod(
            "BuildLogDetails",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var output = new Dictionary<string, object>
        {
            ["requestMethod"] = "POST",
            ["requestUrl"] = "https://example.test/pub/v1/app/orders/order/list",
            ["requestBody"] = """{"filter":{"tf":{"status":"active"}}}""",
            ["statusCode"] = 400,
            ["responseBody"] = """{"error":"bad filter"}""",
        };

        var details = (string?)method!.Invoke(null, new object[] { output });

        details.Should().NotBeNull();
        details.Should().Contain("POST");
        details.Should().Contain("https://example.test/pub/v1/app/orders/order/list");
        details.Should().Contain("""
            {"filter":{"tf":{"status":"active"}}}
            """);
        details.Should().Contain("HTTP 400");
        details.Should().Contain("""{"error":"bad filter"}""");
    }
}
