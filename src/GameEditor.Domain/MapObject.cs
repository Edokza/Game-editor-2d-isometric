namespace GameEditor.Domain;

/// <summary><see cref="X"/>, <see cref="Y"/> in tile space (tile (x,y) center = x+0.5, y+0.5).
/// <see cref="Sprite"/> = file name in assets/objects without extension; null or missing file = placeholder box.</summary>
public sealed record MapObject(int Id, string Name, float X, float Y, string? Sprite = null);
