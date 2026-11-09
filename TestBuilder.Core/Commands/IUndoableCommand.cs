namespace TestBuilder.Core.Commands;

/// <summary>
/// Обратимая команда для стека Undo/Redo.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Описание команды для UI.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Выполняет команду.
    /// </summary>
    void Do();

    /// <summary>
    /// Отменяет изменения, выполненные командой.
    /// </summary>
    void Undo();
}
