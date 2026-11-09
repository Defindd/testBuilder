namespace TestBuilder.Core.Execution;

/// <summary>
/// Состояние выполнения шага.
/// </summary>
public enum StepStatus
{
    /// <summary>Шаг ещё не выполнялся.</summary>
    Pending,

    /// <summary>Шаг выполняется.</summary>
    Running,

    /// <summary>Шаг завершился успешно.</summary>
    Success,

    /// <summary>Шаг завершился ошибкой.</summary>
    Failed,

    /// <summary>Шаг пропущен.</summary>
    Skipped,

    /// <summary>Выполнение шага отменено.</summary>
    Cancelled,
}
