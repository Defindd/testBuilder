using TestBuilder.Core.Blocks;
using TestBuilder.Core.Commands;
using TestBuilder.Core.Scenarios;

namespace TestBuilder.UI.Commands;

/// <summary>
/// Команда добавления блока в сценарий.
/// </summary>
public sealed class AddBlockCommand : IUndoableCommand
{
    private readonly TestScenario _scenario;
    private readonly ITestBlock _block;
    private readonly int _index;

    /// <summary>
    /// Создаёт команду добавления.
    /// </summary>
    /// <param name="scenario">Сценарий.</param>
    /// <param name="block">Добавляемый блок.</param>
    /// <param name="index">Позиция вставки.</param>
    public AddBlockCommand(TestScenario scenario, ITestBlock block, int index)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _block = block ?? throw new ArgumentNullException(nameof(block));
        _index = index;
    }

    /// <inheritdoc />
    public string Description => $"Добавить блок «{_block.Title}»";

    /// <inheritdoc />
    public void Do()
    {
        _scenario.Blocks.Insert(Math.Clamp(_index, 0, _scenario.Blocks.Count), _block);
    }

    /// <inheritdoc />
    public void Undo()
    {
        var actualIndex = _scenario.Blocks.IndexOf(_block);
        if (actualIndex >= 0)
        {
            _scenario.Blocks.RemoveAt(actualIndex);
        }
    }
}

/// <summary>
/// Команда удаления блока из сценария.
/// </summary>
public sealed class RemoveBlockCommand : IUndoableCommand
{
    private readonly TestScenario _scenario;
    private readonly ITestBlock _block;
    private readonly int _index;

    /// <summary>
    /// Создаёт команду удаления.
    /// </summary>
    /// <param name="scenario">Сценарий.</param>
    /// <param name="block">Удаляемый блок.</param>
    /// <param name="index">Позиция блока.</param>
    public RemoveBlockCommand(TestScenario scenario, ITestBlock block, int index)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _block = block ?? throw new ArgumentNullException(nameof(block));
        _index = index;
    }

    /// <inheritdoc />
    public string Description => $"Удалить блок «{_block.Title}»";

    /// <inheritdoc />
    public void Do()
    {
        var actualIndex = _scenario.Blocks.IndexOf(_block);
        if (actualIndex >= 0)
        {
            _scenario.Blocks.RemoveAt(actualIndex);
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        _scenario.Blocks.Insert(Math.Clamp(_index, 0, _scenario.Blocks.Count), _block);
    }
}

/// <summary>
/// Команда перемещения блока внутри сценария.
/// </summary>
public sealed class MoveBlockCommand : IUndoableCommand
{
    private readonly TestScenario _scenario;
    private readonly int _fromIndex;
    private readonly int _toIndex;

    /// <summary>
    /// Создаёт команду перемещения.
    /// </summary>
    /// <param name="scenario">Сценарий.</param>
    /// <param name="fromIndex">Текущая позиция.</param>
    /// <param name="toIndex">Новая позиция.</param>
    public MoveBlockCommand(TestScenario scenario, int fromIndex, int toIndex)
    {
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _fromIndex = fromIndex;
        _toIndex = toIndex;
    }

    /// <inheritdoc />
    public string Description => "Переместить блок";

    /// <inheritdoc />
    public void Do() => Move(_fromIndex, _toIndex);

    /// <inheritdoc />
    public void Undo() => Move(_toIndex, _fromIndex);

    private void Move(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 ||
            fromIndex >= _scenario.Blocks.Count ||
            toIndex < 0 ||
            toIndex >= _scenario.Blocks.Count ||
            fromIndex == toIndex)
        {
            return;
        }

        var block = _scenario.Blocks[fromIndex];
        _scenario.Blocks.RemoveAt(fromIndex);
        _scenario.Blocks.Insert(toIndex, block);
    }
}
