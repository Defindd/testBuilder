using System.Text.Json;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Serialization;

namespace TestBuilder.Core.Reports;

/// <summary>
/// Экспортирует отчёт в читаемый JSON.
/// </summary>
public sealed class JsonReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSetup.CreateDefault();

    /// <inheritdoc />
    public string Format => "json";

    /// <inheritdoc />
    public void Export(RunReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        JsonSerializer.Serialize(output, report, SerializerOptions);
        output.Flush();
    }
}
