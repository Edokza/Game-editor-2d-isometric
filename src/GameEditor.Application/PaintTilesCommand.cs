using GameEditor.Domain;

namespace GameEditor.Application;

public sealed class PaintTilesCommand(TileMap map, IReadOnlyDictionary<TileCoord, (int Old, int New)> changes) : ICommand
{
    public void Execute()
    {
        foreach (var (c, v) in changes) map.Set(c, v.New);
    }

    public void Undo()
    {
        foreach (var (c, v) in changes) map.Set(c, v.Old);
    }
}
