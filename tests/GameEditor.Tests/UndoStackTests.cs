using GameEditor.Application;

namespace GameEditor.Tests;

public class UndoStackTests
{
    private sealed class LogCommand(string name, List<string> log) : ICommand
    {
        public void Execute() => log.Add("do " + name);
        public void Undo() => log.Add("undo " + name);
    }

    [Fact]
    public void UndoRedo_Order()
    {
        var log = new List<string>();
        var s = new UndoStack();
        s.Push(new LogCommand("a", log));
        s.Push(new LogCommand("b", log));

        Assert.True(s.Undo());
        Assert.True(s.Undo());
        Assert.False(s.Undo());
        Assert.True(s.Redo());
        Assert.True(s.Redo());
        Assert.False(s.Redo());

        Assert.Equal(["undo b", "undo a", "do a", "do b"], log);
    }

    [Fact]
    public void Push_ClearsRedo()
    {
        var log = new List<string>();
        var s = new UndoStack();
        s.Push(new LogCommand("a", log));
        s.Undo();
        Assert.True(s.CanRedo);
        s.Push(new LogCommand("b", log));
        Assert.False(s.CanRedo);
    }

    [Fact]
    public void Clear_EmptiesBoth()
    {
        var s = new UndoStack();
        s.Push(new LogCommand("a", []));
        s.Push(new LogCommand("b", []));
        s.Undo();
        s.Clear();
        Assert.False(s.CanUndo);
        Assert.False(s.CanRedo);
    }
}
