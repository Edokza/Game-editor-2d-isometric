namespace GameEditor.Domain;

/// <summary><see cref="X"/>, <see cref="Y"/> in tile space (tile (x,y) center = x+0.5, y+0.5).</summary>
public sealed record MapObject(int Id, string Name, float X, float Y);
