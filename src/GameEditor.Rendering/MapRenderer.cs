using System.Numerics;
using GameEditor.Domain;
using Raylib_cs;

namespace GameEditor.Rendering;

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
    ];

    private static readonly Color Outline = new(0, 0, 0, 60);

    public static Color ColorOf(int tileId) => (uint)tileId < (uint)Palette.Length ? Palette[tileId] : Color.Magenta;

    /// <summary><paramref name="origin"/> = screen position of tile (0,0) top vertex.</summary>
    public static void Draw(TileMap map, Vector2 origin)
    {
        var right = new Vector2(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var left = new Vector2(-right.X, right.Y);
        var down = new Vector2(0, IsoMath.TileHeight);

        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new TileCoord(x, y);
            var top = origin + IsoMath.TileToScreen(c);
            var color = ColorOf(map.Get(c));
            Raylib.DrawTriangle(top, top + left, top + down, color);
            Raylib.DrawTriangle(top, top + down, top + right, color);
            Raylib.DrawLineV(top, top + right, Outline);
            Raylib.DrawLineV(top, top + left, Outline);
        }
    }

    public static void DrawHighlight(TileCoord c, Vector2 origin, Color color)
    {
        var top = origin + IsoMath.TileToScreen(c);
        var right = top + new Vector2(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var left = top + new Vector2(-IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var bottom = top + new Vector2(0, IsoMath.TileHeight);
        Raylib.DrawLineV(top, right, color);
        Raylib.DrawLineV(right, bottom, color);
        Raylib.DrawLineV(bottom, left, color);
        Raylib.DrawLineV(left, top, color);
    }

    /// <summary>Placeholder box until sprites: bottom center = object position.</summary>
    public static Rectangle ObjectRect(MapObject o, Vector2 origin)
    {
        const float W = 24, H = 40;
        var foot = origin + IsoMath.TileToScreen(o.X, o.Y);
        return new Rectangle(foot.X - W / 2, foot.Y - H, W, H);
    }

    public static void DrawObjects(IReadOnlyList<MapObject> objects, Vector2 origin, int? selectedId)
    {
        foreach (var o in DrawOrder(objects))
        {
            var rect = ObjectRect(o, origin);
            Raylib.DrawRectangleRec(rect, Raylib.ColorFromHSV(o.Id * 67 % 360, 0.6f, 0.9f));
            Raylib.DrawRectangleLinesEx(rect, 1, Color.Black);
            if (o.Id == selectedId) Raylib.DrawRectangleLinesEx(rect, 2, Color.Yellow);
        }
    }

    /// <summary>Front-most object under <paramref name="p"/> (same origin as drawing), or null.</summary>
    public static MapObject? ObjectAt(IReadOnlyList<MapObject> objects, Vector2 origin, Vector2 p) =>
        DrawOrder(objects).LastOrDefault(o => Raylib.CheckCollisionPointRec(p, ObjectRect(o, origin)));

    private static readonly List<MapObject> Sorted = []; // reused: no per-frame allocation

    // Y-sort: larger X+Y = nearer the viewer = drawn later; ties by Id so the order is the same every frame
    private static readonly Comparison<MapObject> ByDepth = (a, b) =>
        (a.X + a.Y).CompareTo(b.X + b.Y) is var c and not 0 ? c : a.Id.CompareTo(b.Id);

    /// <summary>Shared buffer: valid until the next call.</summary>
    private static List<MapObject> DrawOrder(IReadOnlyList<MapObject> objects)
    {
        Sorted.Clear();
        Sorted.AddRange(objects);
        Sorted.Sort(ByDepth);
        return Sorted;
    }
}
