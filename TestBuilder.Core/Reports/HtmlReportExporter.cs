using System.Globalization;
using System.Net;
using System.Text;
using TestBuilder.Core.Execution;

namespace TestBuilder.Core.Reports;

/// <summary>
/// Экспортирует автономный HTML-отчёт с таблицей шагов и цветовой подсветкой.
/// </summary>
public sealed class HtmlReportExporter : IReportExporter
{
    /// <inheritdoc />
    public string Format => "html";

    /// <inheritdoc />
    public void Export(RunReport report, Stream output)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(output);

        var html = BuildHtml(report);
        using var writer = new StreamWriter(
            output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 1024,
            leaveOpen: true);

        writer.Write(html);
        writer.Flush();
    }

    private static string BuildHtml(RunReport report)
    {
        var builder = new StringBuilder(capacity: 8192);
        var passed = report.AllPassed;
        var summaryClass = passed ? "success" : "failed";
        var summaryText = passed ? "Пройдено" : "Есть ошибки";

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"ru\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\" />");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        builder.AppendLine($"  <title>Test Builder — {Html(report.ScenarioName)}</title>");
        builder.AppendLine("  <style>");
        builder.AppendLine("    :root { color-scheme: light; --border: #d9dee7; --muted: #5d6675; --bg: #f6f8fb; }");
        builder.AppendLine("    * { box-sizing: border-box; }");
        builder.AppendLine("    body { margin: 0; padding: 32px; font-family: 'Segoe UI', Arial, sans-serif; background: var(--bg); color: #1e2430; }");
        builder.AppendLine("    .card { max-width: 1180px; margin: 0 auto; background: #fff; border: 1px solid var(--border); border-radius: 12px; box-shadow: 0 8px 24px rgba(20, 35, 60, .08); overflow: hidden; }");
        builder.AppendLine("    header { padding: 24px 28px; border-bottom: 1px solid var(--border); }");
        builder.AppendLine("    h1 { margin: 0 0 6px; font-size: 24px; }");
        builder.AppendLine("    .meta { color: var(--muted); font-size: 14px; }");
        builder.AppendLine("    .badge { display: inline-block; margin-top: 14px; padding: 6px 12px; border-radius: 999px; font-weight: 600; font-size: 13px; }");
        builder.AppendLine("    .badge.success { background: #d9f5e3; color: #17643a; }");
        builder.AppendLine("    .badge.failed { background: #ffe0df; color: #a12828; }");
        builder.AppendLine("    table { width: 100%; border-collapse: collapse; font-size: 14px; }");
        builder.AppendLine("    th, td { padding: 12px 14px; border-bottom: 1px solid var(--border); text-align: left; vertical-align: top; }");
        builder.AppendLine("    th { background: #eef2f8; font-weight: 600; white-space: nowrap; }");
        builder.AppendLine("    tr:last-child td { border-bottom: 0; }");
        builder.AppendLine("    td.status-success { background: #e9f9ef; }");
        builder.AppendLine("    td.status-failed { background: #fff0ef; }");
        builder.AppendLine("    td.status-cancelled { background: #fff7e6; }");
        builder.AppendLine("    td.status-skipped { background: #f1f3f6; }");
        builder.AppendLine("    .status { font-weight: 600; white-space: nowrap; }");
        builder.AppendLine("    .message { white-space: pre-wrap; overflow-wrap: anywhere; }");
        builder.AppendLine("    .empty { padding: 24px 28px; color: var(--muted); }");
        builder.AppendLine("  </style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <main class=\"card\">");
        builder.AppendLine("    <header>");
        builder.AppendLine($"      <h1>{Html(report.ScenarioName)}</h1>");
        builder.AppendLine($"      <div class=\"meta\">Начало: {Html(report.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))} · Длительность: {Html(FormatDuration(report.TotalDuration))} · Шагов: {report.Steps.Count}</div>");
        builder.AppendLine($"      <span class=\"badge {summaryClass}\">{summaryText}</span>");
        builder.AppendLine("    </header>");

        if (report.Steps.Count == 0)
        {
            builder.AppendLine("    <div class=\"empty\">Сценарий не содержит выполненных шагов.</div>");
        }
        else
        {
            builder.AppendLine("    <table>");
            builder.AppendLine("      <thead><tr><th>#</th><th>Шаг</th><th>Тип</th><th>Статус</th><th>Длительность</th><th>Сообщение</th></tr></thead>");
            builder.AppendLine("      <tbody>");

            for (var index = 0; index < report.Steps.Count; index++)
            {
                var step = report.Steps[index];
                var statusClass = GetStatusClass(step.Status);
                builder.AppendLine("        <tr>");
                builder.AppendLine($"          <td>{index + 1}</td>");
                builder.AppendLine($"          <td>{Html(step.Title)}</td>");
                builder.AppendLine($"          <td>{Html(step.BlockType)}</td>");
                builder.AppendLine($"          <td class=\"status-{statusClass}\"><span class=\"status\">{Html(GetStatusText(step.Status))}</span></td>");
                builder.AppendLine($"          <td>{Html(FormatDuration(step.Duration))}</td>");
                builder.AppendLine($"          <td class=\"message\">{Html(step.Message)}</td>");
                builder.AppendLine("        </tr>");
            }

            builder.AppendLine("      </tbody>");
            builder.AppendLine("    </table>");
        }

        builder.AppendLine("  </main>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private static string GetStatusClass(StepStatus status)
    {
        return status switch
        {
            StepStatus.Success => "success",
            StepStatus.Failed => "failed",
            StepStatus.Cancelled => "cancelled",
            StepStatus.Skipped => "skipped",
            _ => "pending",
        };
    }

    private static string GetStatusText(StepStatus status)
    {
        return status switch
        {
            StepStatus.Success => "Успех",
            StepStatus.Failed => "Ошибка",
            StepStatus.Cancelled => "Отменён",
            StepStatus.Skipped => "Пропущен",
            StepStatus.Running => "Выполняется",
            _ => "Ожидает",
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + " с";
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}
