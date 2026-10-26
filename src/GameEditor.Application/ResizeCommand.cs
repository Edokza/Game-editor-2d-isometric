using GameEditor.Domain;

namespace GameEditor.Application;

/// <summary>Swaps whole <see cref="TileMap"/> instances, so older commands still hold the map they edit.</summary>
public sealed class ResizeCommand(Action<TileMap> set, TileMap before, TileMap after) : ICommand
{
    public void Execute() => set(after);
    public void Undo() => set(before);
}
