using System.Globalization;
using System.Text;
using System.Xml;
using TestBuilder.Core.Execution;

namespace TestBuilder.Core.Reports;

/// <summary>
/// Экспортирует отчёт в JUnit XML, совместимый с CI-системами.
/// </summary>
public sealed class JUnitXmlReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => "junit";

    /// <inheritdoc />
    public void Export(RunReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var failureCount = report.Steps.Count(step =>
            step.Status is StepStatus.Failed or StepStatus.Cancelled or StepStatus.Running or StepStatus.Pending);
        var skippedCount = report.Steps.Count(step => step.Status == StepStatus.Skipped);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            CloseOutput = false,
            OmitXmlDeclaration = false,
        };

        using var writer = XmlWriter.Create(output, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("testsuites");
        writer.WriteAttributeString("name", "TestBuilder");
        writer.WriteAttributeString("tests", report.Steps.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("failures", failureCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("errors", "0");
        writer.WriteAttributeString("skipped", skippedCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("time", Seconds(report.TotalDuration));

        writer.WriteStartElement("testsuite");
        writer.WriteAttributeString("name", report.ScenarioName);
        writer.WriteAttributeString("tests", report.Steps.Count.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("failures", failureCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("errors", "0");
        writer.WriteAttributeString("skipped", skippedCount.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("time", Seconds(report.TotalDuration));
        writer.WriteAttributeString("timestamp", report.StartedAt.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteAttributeString("hostname", System.Environment.MachineName);

        foreach (var step in report.Steps)
        {
            writer.WriteStartElement("testcase");
            writer.WriteAttributeString("name", step.Title);
            writer.WriteAttributeString("classname", string.IsNullOrWhiteSpace(step.BlockType) ? "TestBuilder" : step.BlockType);
            writer.WriteAttributeString("time", Seconds(step.Duration));

            switch (step.Status)
            {
                case StepStatus.Skipped:
                    writer.WriteStartElement("skipped");
                    writer.WriteEndElement();
                    break;

                case StepStatus.Success:
                    break;

                default:
                    writer.WriteStartElement("failure");
                    writer.WriteAttributeString("message", Truncate(step.Message, 2000));
                    writer.WriteAttributeString("type", step.Status.ToString());
                    writer.WriteString(step.Message);
                    writer.WriteEndElement();
                    break;
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
    }

    private static string Seconds(TimeSpan duration)
    {
        return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}
