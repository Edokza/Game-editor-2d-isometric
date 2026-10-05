using GameEditor.Domain;

namespace GameEditor.Application;

/// <summary>May throw; <see cref="MapEditingService"/> turns failures into <see cref="Result"/>.</summary>
public interface IMapRepository
{
    void Save(TileMap map, string name);
    TileMap Load(string name);
    IReadOnlyList<string> List();
}
