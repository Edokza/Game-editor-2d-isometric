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

    /// <summary>Flat tiles only; tall ones (blocks) are depth-sorted with objects in <see cref="DrawObjects"/>.
    /// <paramref name="origin"/> = screen position of tile (0,0) top vertex.</summary>
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
            int id = map.Get(c);
            var tex = Sprites.Tile(id);
            if (tex.Id != 0)
            {
                if (!IsTall(tex)) DrawTile(tex, top);
                continue;
            }
            var color = ColorOf(id);
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

    /// <summary>Sprite bounds (24×40 placeholder box without one); bottom center = object position.</summary>
    public static Rectangle ObjectRect(MapObject o, Vector2 origin)
    {
        var tex = Sprites.Object(o.Sprite);
        float w = tex.Id != 0 ? tex.Width : 24, h = tex.Id != 0 ? tex.Height : 40;
        var foot = origin + IsoMath.TileToScreen(o.X, o.Y);
        // floor: odd widths and fractional positions would land texels between screen pixels
        return new Rectangle(MathF.Floor(foot.X - w / 2), MathF.Floor(foot.Y - h), w, h);
    }

    private static bool IsTall(Texture2D tex) => tex.Height > IsoMath.TileHeight;

    // image bottom center = tile bottom vertex
    private static void DrawTile(Texture2D tex, Vector2 top) =>
        Raylib.DrawTextureV(tex, top + new Vector2(-tex.Width / 2f, IsoMath.TileHeight - tex.Height), Color.White);

    /// <summary>Block silhouette = tile diamond swept up to the image top; not the image rect, whose transparent corners show what is behind.</summary>
    private static bool BlockCovers(Texture2D tex, Vector2 top, Vector2 p)
    {
        float dx = MathF.Abs(p.X - top.X), slope = dx * IsoMath.TileHeight / IsoMath.TileWidth;
        float bottom = top.Y + IsoMath.TileHeight;
        return dx <= IsoMath.TileWidth / 2f && p.Y >= bottom - tex.Height + slope && p.Y <= bottom - slope;
    }

    /// <summary>Objects and tall tiles back to front, after <see cref="Draw"/>.</summary>
    public static void DrawObjects(TileMap map, Vector2 origin, int? selectedId)
    {
        var sorted = DrawOrder(map.Objects);
        int k = 0;
        // diagonal s = x + y: a tall tile's depth is its center, s + 1; tiles on one diagonal never overlap each other
        for (int s = 0; s <= map.Width + map.Height - 2; s++)
        {
            while (k < sorted.Count && sorted[k].X + sorted[k].Y < s + 1) DrawObject(sorted[k++], origin, selectedId);
            for (int x = Math.Max(0, s - map.Height + 1); x <= Math.Min(s, map.Width - 1); x++)
            {
                var c = new TileCoord(x, s - x);
                var tex = Sprites.Tile(map.Get(c));
                if (tex.Id != 0 && IsTall(tex)) DrawTile(tex, origin + IsoMath.TileToScreen(c));
            }
        }
        while (k < sorted.Count) DrawObject(sorted[k++], origin, selectedId);
    }

    private static void DrawObject(MapObject o, Vector2 origin, int? selectedId)
    {
        var rect = ObjectRect(o, origin);
        var tex = Sprites.Object(o.Sprite);
        if (tex.Id != 0) Raylib.DrawTextureV(tex, rect.Position, Color.White);
        else
        {
            Raylib.DrawRectangleRec(rect, Raylib.ColorFromHSV(o.Id * 67 % 360, 0.6f, 0.9f));
            Raylib.DrawRectangleLinesEx(rect, 1, Color.Black);
        }
        if (o.Id == selectedId) Raylib.DrawRectangleLinesEx(rect, 2, Color.Yellow);
    }

    /// <summary>Front-most object under <paramref name="p"/> (same origin as drawing), or null when none is there
    /// or a tall tile <see cref="DrawObjects"/> draws later covers it, so a click on a block paints instead of grabbing what it hides.</summary>
    public static MapObject? ObjectAt(TileMap map, Vector2 origin, Vector2 p)
    {
        var hit = DrawOrder(map.Objects).LastOrDefault(o => Raylib.CheckCollisionPointRec(p, ObjectRect(o, origin)));
        if (hit is null) return null;
        // full scan: runs once per click, not per frame. Objects behind hit are drawn before the same tiles, so covered too
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            if (hit.X + hit.Y >= x + y + 1) continue; // same test as DrawObjects: this tile is drawn before hit
            var c = new TileCoord(x, y);
            var tex = Sprites.Tile(map.Get(c));
            if (tex.Id != 0 && IsTall(tex) && BlockCovers(tex, origin + IsoMath.TileToScreen(c), p)) return null;
        }
        return hit;
    }

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
