using FluentAssertions;
using TestBuilder.Core.Commands;
using Xunit;

namespace TestBuilder.Tests.Commands;

/// <summary>
/// Проверки стека Undo/Redo.
/// </summary>
public sealed class UndoRedoStackTests
{
    [Fact]
    public void PushUndoRedo_ChangesStateAndInvokesChanged()
    {
        var value = 0;
        var changedCount = 0;
        var stack = new UndoRedoStack();
        stack.Changed += (_, _) => changedCount++;

        stack.Push(new DelegateCommand("+1", () => value++, () => value--));

        value.Should().Be(1);
        stack.CanUndo.Should().BeTrue();
        stack.UndoDescription.Should().Be("+1");

        stack.Undo();
        value.Should().Be(0);
        stack.CanRedo.Should().BeTrue();

        stack.Redo();
        value.Should().Be(1);
        stack.CanRedo.Should().BeFalse();
        changedCount.Should().Be(3);
    }

    [Fact]
    public void Push_AfterUndo_ClearsRedoStack()
    {
        var stack = new UndoRedoStack();
        stack.Push(new DelegateCommand("first", () => { }, () => { }));
        stack.Undo();

        stack.CanRedo.Should().BeTrue();

        stack.Push(new DelegateCommand("second", () => { }, () => { }));

        stack.CanRedo.Should().BeFalse();
    }

    private sealed class DelegateCommand : IUndoableCommand
    {
        private readonly Action _do;
        private readonly Action _undo;

        public DelegateCommand(string description, Action @do, Action undo)
        {
            Description = description;
            _do = @do;
            _undo = undo;
        }

        public string Description { get; }

        public void Do() => _do();

        public void Undo() => _undo();
    }
}
