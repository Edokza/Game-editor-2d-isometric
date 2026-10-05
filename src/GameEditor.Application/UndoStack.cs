namespace GameEditor.Application;

public sealed class UndoStack
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records an already-executed command. Clears redo.</summary>
    public void Push(ICommand command)
    {
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var c)) return false;
        c.Undo();
        _redo.Push(c);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var c)) return false;
        c.Execute();
        _undo.Push(c);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
