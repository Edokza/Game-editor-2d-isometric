using GameEditor.Domain;

namespace GameEditor.Application;

/// <summary>null → obj = add, obj → null = remove, obj → obj' = edit; all at <paramref name="index"/>.</summary>
public sealed class ObjectCommand(TileMap map, int index, MapObject? before, MapObject? after) : ICommand
{
    public void Execute() => Apply(before, after);
    public void Undo() => Apply(after, before);

    private void Apply(MapObject? from, MapObject? to)
    {
        if (to is null) map.RemoveAt(index);
        else if (from is null) map.Insert(index, to);
        else map.Replace(index, to);
    }
}
