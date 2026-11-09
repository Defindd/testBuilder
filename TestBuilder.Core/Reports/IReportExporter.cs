using TestBuilder.Core.Execution;

namespace TestBuilder.Core.Reports;

/// <summary>
/// Экспортёр отчёта о выполнении сценария.
/// </summary>
public interface IReportExporter
{
    /// <summary>
    /// Идентификатор формата: <c>json</c>, <c>html</c>, <c>junit</c>.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Записывает отчёт в поток. Поток не закрывается.
    /// </summary>
    /// <param name="report">Отчёт.</param>
    /// <param name="output">Целевой поток.</param>
    void Export(RunReport report, Stream output);
}
