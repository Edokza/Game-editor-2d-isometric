using Raylib_cs;

namespace GameEditor.Rendering;

/// <summary>Tile colors (no-texture fallback, wall shades) and the screen background; the map itself is <see cref="Renderer3D"/>.</summary>
public static class MapRenderer
{
    /// <summary>Index = tileId.</summary>
    public static readonly Color[] Palette =
    [
        new(76, 153, 76, 255),   // 0 grass
        new(64, 120, 200, 255),  // 1 water
        new(220, 200, 130, 255), // 2 sand
        new(130, 130, 140, 255), // 3 stone
        new(120, 85, 55, 255),   // 4 dirt
        new(235, 240, 245, 255), // 5 snow
        new(120, 85, 55, 255),   // 6 dirt block: its 3D walls
        new(130, 130, 140, 255), // 7 stone block
    ];

    public static Color ColorOf(int tileId) => (uint)tileId < (uint)Palette.Length ? Palette[tileId] : Color.Magenta;

    /// <summary>Dark blue top → grey-purple bottom; screen space, call outside BeginMode2D/3D. Also clears depth: 3D needs it every frame.</summary>
    public static void DrawBackground(int w, int h)
    {
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawRectangleGradientV(0, 0, w, h, new Color(18, 24, 52, 255), new Color(78, 70, 96, 255));
    }
}
