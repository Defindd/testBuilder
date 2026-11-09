using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Reports;
using Xunit;

namespace TestBuilder.Tests.Reports;

/// <summary>
/// Проверки экспортёров отчётов.
/// </summary>
public sealed class ReportExporterTests
{
    [Fact]
    public void JsonExporter_WritesValidReport()
    {
        var report = CreateReport();

        using var stream = new MemoryStream();
        new JsonReportExporter().Export(report, stream);

        stream.Position = 0;
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        root.GetProperty("scenarioName").GetString().Should().Be("Smoke <test>");
        root.GetProperty("allPassed").GetBoolean().Should().BeFalse();
        root.GetProperty("steps").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void JUnitExporter_ProducesTestsuitesAndFailureElement()
    {
        var report = CreateReport();

        using var stream = new MemoryStream();
        new JUnitXmlReportExporter().Export(report, stream);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();

        xml.Should().Contain("<testsuites");
        xml.Should().Contain("<testcase");
        xml.Should().Contain("<failure");

        var document = XDocument.Parse(xml);
        document.Root!.Name.LocalName.Should().Be("testsuites");
    }

    [Fact]
    public void HtmlExporter_EscapesTitleAndHighlightsFailure()
    {
        var report = CreateReport();

        using var stream = new MemoryStream();
        new HtmlReportExporter().Export(report, stream);

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        html.Should().Contain("status-failed");
        html.Should().Contain("Smoke &lt;test&gt;");
        html.Should().Contain("<!DOCTYPE html>");
    }

    private static RunReport CreateReport()
    {
        return new RunReport
        {
            ScenarioName = "Smoke <test>",
            StartedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            TotalDuration = TimeSpan.FromSeconds(1.5),
            Steps =
            {
                new StepResult
                {
                    BlockId = Guid.NewGuid(),
                    Title = "Успешный шаг",
                    BlockType = "fake",
                    Status = StepStatus.Success,
                    Duration = TimeSpan.FromMilliseconds(100),
                    Message = "OK",
                },
                new StepResult
                {
                    BlockId = Guid.NewGuid(),
                    Title = "Неуспешный шаг",
                    BlockType = "fake",
                    Status = StepStatus.Failed,
                    Duration = TimeSpan.FromMilliseconds(200),
                    Message = "Ожидалось 1, получено 2",
                },
            },
        };
    }
}
