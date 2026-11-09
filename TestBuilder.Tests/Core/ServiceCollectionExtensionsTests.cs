using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.DependencyInjection;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Scenarios;
using TestBuilder.Core.Templating;
using Xunit;

namespace TestBuilder.Tests.Core;

/// <summary>
/// Проверки композиционного корня ядра.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddTestBuilderCore_RegistersAllCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddTestBuilderCore(options =>
        {
            options.DefaultHttpTimeout = TimeSpan.FromSeconds(5);
        });

        using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<IBlockRegistry>().Should().NotBeNull();
        provider.GetRequiredService<TemplateEngine>().Should().NotBeNull();
        provider.GetRequiredService<ScenarioService>().Should().NotBeNull();
        provider.GetRequiredService<TestRunner>().Should().NotBeNull();
        provider.GetRequiredService<IHttpClientFactory>().Should().NotBeNull();
    }
}
