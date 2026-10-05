namespace GameEditor.Application;

public interface ICommand
{
    void Execute();
    void Undo();
}
