using System.Numerics;

namespace GameEditor.Domain;

/// <summary>64×32 diamond tiles. Screen point of a tile = its top vertex; tile (0,0) top vertex at origin.</summary>
public static class IsoMath
{
    public const int TileWidth = 64;
    public const int TileHeight = 32;
    private const float HalfW = TileWidth / 2f;
    private const float HalfH = TileHeight / 2f;

    public static Vector2 TileToScreen(TileCoord c) => new((c.X - c.Y) * HalfW, (c.X + c.Y) * HalfH);

    public static TileCoord ScreenToTile(Vector2 p)
    {
        float a = p.X / HalfW, b = p.Y / HalfH;
        return new((int)MathF.Floor((b + a) / 2), (int)MathF.Floor((b - a) / 2));
    }
}
