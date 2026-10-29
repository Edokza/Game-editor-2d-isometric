using GameEditor.Domain;

namespace GameEditor.Application;

/// <summary>Tile ids and/or heights changed by one stroke, fill or box: one undo step.</summary>
public sealed class PaintTilesCommand(TileMap map, IReadOnlyDictionary<TileCoord, (int Old, int New)> changes,
    IReadOnlyDictionary<TileCoord, (int Old, int New)>? heights = null) : ICommand
{
    private readonly IReadOnlyDictionary<TileCoord, (int Old, int New)> _heights = heights ?? new Dictionary<TileCoord, (int Old, int New)>();

    public void Execute()
    {
        foreach (var (c, v) in changes) map.Set(c, v.New);
        foreach (var (c, v) in _heights) map.SetHeight(c, v.New);
    }

    public void Undo()
    {
        foreach (var (c, v) in changes) map.Set(c, v.Old);
        foreach (var (c, v) in _heights) map.SetHeight(c, v.Old);
    }
}
