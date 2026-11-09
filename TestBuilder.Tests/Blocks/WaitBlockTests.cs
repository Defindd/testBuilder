using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TestBuilder.Blocks.Wait;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Templating;
using ExecutionContext = TestBuilder.Core.Execution.ExecutionContext;
using Xunit;

namespace TestBuilder.Tests.Blocks;

/// <summary>
/// Тесты блока ожидания.
/// </summary>
public sealed class WaitBlockTests
{
    [Fact]
    public async Task Fixed_WithDuration_ReturnsSuccessAfterDelay()
    {
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var block = new WaitBlock
        {
            Duration = TimeSpan.FromMilliseconds(150),
            JitterMs = 0,
        };

        var result = await block.ExecuteAsync(CreateContext(httpClient, cts.Token));

        result.Success.Should().BeTrue(result.Message);
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100));
        result.Output.Should().ContainKey("waitedMs");
    }

    [Fact]
    public async Task Fixed_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var block = new WaitBlock
        {
            Duration = TimeSpan.FromSeconds(5),
            JitterMs = 0,
        };

        Func<Task> action = () => block.ExecuteAsync(CreateContext(httpClient, cts.Token));

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UntilCondition_WithSatisfiedCondition_ReturnsSuccessBeforeMaxWait()
    {
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var block = new WaitBlock
        {
            Mode = WaitMode.UntilCondition,
            ConditionExpression = "{{x}} == 5",
            MaxWait = TimeSpan.FromSeconds(5),
            PollInterval = TimeSpan.FromMilliseconds(20),
        };
        var variables = new Dictionary<string, object> { ["x"] = 5 };

        var result = await block.ExecuteAsync(CreateContext(httpClient, cts.Token, variables));

        result.Success.Should().BeTrue(result.Message);
        result.Duration.Should().BeLessThan(block.MaxWait);
    }

    [Fact]
    public async Task UntilCondition_WhenConditionNeverTrue_ReturnsFailureAfterMaxWait()
    {
        using var httpClient = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var block = new WaitBlock
        {
            Mode = WaitMode.UntilCondition,
            ConditionExpression = "{{x}} == 999",
            MaxWait = TimeSpan.FromMilliseconds(250),
            PollInterval = TimeSpan.FromMilliseconds(25),
        };
        var variables = new Dictionary<string, object> { ["x"] = 5 };

        var stopwatch = Stopwatch.StartNew();
        var result = await block.ExecuteAsync(CreateContext(httpClient, cts.Token, variables));
        stopwatch.Stop();

        result.Success.Should().BeFalse();
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(200));
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(200));
        result.Message.Should().Contain("не стало истинным");
    }

    private static ExecutionContext CreateContext(
        HttpClient httpClient,
        CancellationToken cancellationToken,
        Dictionary<string, object>? variables = null)
    {
        return new ExecutionContext
        {
            HttpClient = httpClient,
            Logger = NullLogger.Instance,
            CancellationToken = cancellationToken,
            Templates = new TemplateEngine(),
            Variables = variables ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
        };
    }
}
