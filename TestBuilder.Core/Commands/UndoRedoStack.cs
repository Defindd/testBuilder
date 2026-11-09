namespace TestBuilder.Core.Commands;

/// <summary>
/// Стек обратимых команд с поддержкой Undo/Redo.
/// </summary>
public sealed class UndoRedoStack
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();
    private readonly object _sync = new();

    /// <summary>
    /// Возникает после любого изменения состояния стека.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Признак доступности отмены.
    /// </summary>
    public bool CanUndo
    {
        get
        {
            lock (_sync)
            {
                return _undo.Count > 0;
            }
        }
    }

    /// <summary>
    /// Признак доступности повтора.
    /// </summary>
    public bool CanRedo
    {
        get
        {
            lock (_sync)
            {
                return _redo.Count > 0;
            }
        }
    }

    /// <summary>
    /// Описание команды, которая будет отменена следующей.
    /// </summary>
    public string? UndoDescription
    {
        get
        {
            lock (_sync)
            {
                return _undo.Count > 0 ? _undo.Peek().Description : null;
            }
        }
    }

    /// <summary>
    /// Описание команды, которая будет повторена следующей.
    /// </summary>
    public string? RedoDescription
    {
        get
        {
            lock (_sync)
            {
                return _redo.Count > 0 ? _redo.Peek().Description : null;
            }
        }
    }

    /// <summary>
    /// Выполняет команду и помещает её в стек отмены.
    /// </summary>
    /// <param name="command">Команда.</param>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> равен <see langword="null"/>.</exception>
    public void Push(IUndoableCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Do();

        lock (_sync)
        {
            _undo.Push(command);
            _redo.Clear();
        }

        OnChanged();
    }

    /// <summary>
    /// Отменяет последнюю команду.
    /// </summary>
    public void Undo()
    {
        IUndoableCommand command;
        lock (_sync)
        {
            if (_undo.Count == 0)
            {
                return;
            }

            command = _undo.Pop();
        }

        try
        {
            command.Undo();
        }
        catch
        {
            lock (_sync)
            {
                _undo.Push(command);
            }

            throw;
        }

        lock (_sync)
        {
            _redo.Push(command);
        }

        OnChanged();
    }

    /// <summary>
    /// Повторяет последнюю отменённую команду.
    /// </summary>
    public void Redo()
    {
        IUndoableCommand command;
        lock (_sync)
        {
            if (_redo.Count == 0)
            {
                return;
            }

            command = _redo.Pop();
        }

        try
        {
            command.Do();
        }
        catch
        {
            lock (_sync)
            {
                _redo.Push(command);
            }

            throw;
        }

        lock (_sync)
        {
            _undo.Push(command);
        }

        OnChanged();
    }

    /// <summary>
    /// Очищает обе стопки, не отменяя команды.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _undo.Clear();
            _redo.Clear();
        }

        OnChanged();
    }

    private void OnChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
