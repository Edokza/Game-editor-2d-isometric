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

    /// <summary>Dark blue top → grey-purple bottom; screen space, call outside BeginMode2D.</summary>
    public static void DrawBackground(int w, int h) =>
        Raylib.DrawRectangleGradientV(0, 0, w, h, new Color(18, 24, 52, 255), new Color(78, 70, 96, 255));

    /// <summary>Flat tiles only; tall ones (blocks) are depth-sorted with objects in <see cref="DrawObjects"/>.
    /// <paramref name="origin"/> = screen position of view tile (0,0) top vertex. <paramref name="view"/> default = unrotated.</summary>
    public static void Draw(TileMap map, Vector2 origin, IsoView view = default)
    {
        var right = new Vector2(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var left = new Vector2(-right.X, right.Y);
        var down = new Vector2(0, IsoMath.TileHeight);
        var wall = new Vector2(0, IsoMath.TileHeight / 2f);

        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new TileCoord(x, y);
            var v = view.ToView(c); // walls and ripple phase follow the screen, not the data axes
            var top = origin + IsoMath.TileToScreen(v);
            int id = map.Get(c);
            var tex = Sprites.Tile(id);
            // water ripple: brightness 0.7–1.0, phase (x′ + y′) → waves run diagonally; subtler was invisible on pastel tiles
            float shade = id == 1 ? -0.15f * (1 + MathF.Sin((float)Raylib.GetTime() * 3 + (v.X + v.Y) * 1.5f)) : 0;
            // front edges get a wall under the diamond edge: map reads as a floating island
            var wallColor = (uint)id < (uint)Palette.Length ? Palette[id] : Palette[4];
            if (v.Y == ViewHeight(map, view) - 1)
            {
                var col = Raylib.ColorBrightness(wallColor, -0.3f);
                Raylib.DrawTriangle(top + left, top + left + wall, top + down + wall, col);
                Raylib.DrawTriangle(top + left, top + down + wall, top + down, col);
            }
            if (v.X == ViewWidth(map, view) - 1)
            {
                var col = Raylib.ColorBrightness(wallColor, -0.5f);
                Raylib.DrawTriangle(top + down, top + down + wall, top + right + wall, col);
                Raylib.DrawTriangle(top + down, top + right + wall, top + right, col);
            }
            if (tex.Id != 0)
            {
                if (!IsTall(tex)) DrawTile(tex, top, Raylib.ColorBrightness(Color.White, shade));
                if (id == 1) DrawRipple(top, v.X + v.Y);
                continue;
            }
            var color = Raylib.ColorBrightness(ColorOf(id), shade);
            Raylib.DrawTriangle(top, top + left, top + down, color);
            Raylib.DrawTriangle(top, top + down, top + right, color);
            Raylib.DrawLineV(top, top + right, Outline);
            Raylib.DrawLineV(top, top + left, Outline);
            if (id == 1) DrawRipple(top, v.X + v.Y);
        }
    }

    /// <summary>Light streak + shadow streak under it, sliding up the tile with the 4.1 wave.
    /// Horizontal: tint phase is x + y = screen Y, so crests line up across neighbours.</summary>
    private static void DrawRipple(Vector2 top, int s)
    {
        // tile spans 2 units of x + y top→bottom = 3 rad of phase; crest where the tint is brightest (sin = -1)
        float p = (float)Raylib.GetTime() * 3 + s * 1.5f;
        float v = ((3 * MathF.PI / 2 - p) % MathF.Tau + MathF.Tau) % MathF.Tau / 3; // 0 top vertex .. 1 bottom
        Streak(top, v, new Color(255, 255, 255, 90));
        Streak(top, v + 0.06f, new Color(20, 40, 90, 70));
    }

    // width 60% of the diamond chord at v: stays inside the tile. Wavy: y bobs with screen X, so it continues into the neighbour
    private static void Streak(Vector2 top, float v, Color color)
    {
        if (v > 1) return;
        float hw = IsoMath.TileWidth / 2f * (1 - MathF.Abs(2 * v - 1)) * 0.6f, y = top.Y + v * IsoMath.TileHeight, t = (float)Raylib.GetTime();
        Vector2 At(float px) => new(px, y + 3 * MathF.Sin(px * 0.1f + t * 2));
        const int n = 8;
        for (int i = 0; i < n; i++)
            Raylib.DrawLineEx(At(top.X - hw + 2 * hw * i / n), At(top.X - hw + 2 * hw * (i + 1) / n), 2, color);
    }

    /// <summary><paramref name="c"/> = data cell.</summary>
    public static void DrawHighlight(TileCoord c, Vector2 origin, Color color, IsoView view = default)
    {
        var top = origin + view.TileToScreen(c);
        var right = top + new Vector2(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var left = top + new Vector2(-IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var bottom = top + new Vector2(0, IsoMath.TileHeight);
        Raylib.DrawLineV(top, right, color);
        Raylib.DrawLineV(right, bottom, color);
        Raylib.DrawLineV(bottom, left, color);
        Raylib.DrawLineV(left, top, color);
    }

    /// <summary>Sprite bounds (48×80 placeholder box without one); bottom center = object position.</summary>
    public static Rectangle ObjectRect(MapObject o, Vector2 origin, IsoView view = default)
    {
        var tex = Sprites.Object(o.Sprite);
        float w = tex.Id != 0 ? tex.Width : 48, h = tex.Id != 0 ? tex.Height : 80;
        var foot = origin + view.TileToScreen(o.X, o.Y);
        // floor: odd widths and fractional positions would land texels between screen pixels
        return new Rectangle(MathF.Floor(foot.X - w / 2), MathF.Floor(foot.Y - h), w, h);
    }

    private static int ViewWidth(TileMap map, IsoView view) => (view.Rotation & 1) == 0 ? map.Width : map.Height;
    private static int ViewHeight(TileMap map, IsoView view) => (view.Rotation & 1) == 0 ? map.Height : map.Width;

    private static bool IsTall(Texture2D tex) => tex.Height > IsoMath.TileHeight;

    // image bottom center = tile bottom vertex
    private static void DrawTile(Texture2D tex, Vector2 top, Color tint) =>
        Raylib.DrawTextureV(tex, top + new Vector2(-tex.Width / 2f, IsoMath.TileHeight - tex.Height), tint);

    /// <summary>Block silhouette = tile diamond swept up to the image top; not the image rect, whose transparent corners show what is behind.</summary>
    private static bool BlockCovers(Texture2D tex, Vector2 top, Vector2 p)
    {
        float dx = MathF.Abs(p.X - top.X), slope = dx * IsoMath.TileHeight / IsoMath.TileWidth;
        float bottom = top.Y + IsoMath.TileHeight;
        return dx <= IsoMath.TileWidth / 2f && p.Y >= bottom - tex.Height + slope && p.Y <= bottom - slope;
    }

    /// <summary>Objects and tall tiles back to front, after <see cref="Draw"/>.</summary>
    public static void DrawObjects(TileMap map, Vector2 origin, int? selectedId, IsoView view = default)
    {
        var sorted = DrawOrder(map.Objects, view);
        int k = 0;
        // from map, not view.ViewWidth: a default view has no size
        bool odd = (view.Rotation & 1) != 0;
        int w = odd ? map.Height : map.Width, h = odd ? map.Width : map.Height;
        // view diagonal s = x′ + y′: a tall tile's depth is its center, s + 1; tiles on one diagonal never overlap each other
        for (int s = 0; s <= w + h - 2; s++)
        {
            while (k < sorted.Count && view.Depth(sorted[k].X, sorted[k].Y) < s + 1) DrawObject(sorted[k++], origin, selectedId, view, map.ShadowlessSprites);
            for (int x = Math.Max(0, s - h + 1); x <= Math.Min(s, w - 1); x++)
            {
                var c = view.FromView(new TileCoord(x, s - x));
                var tex = Sprites.Tile(map.Get(c));
                if (tex.Id != 0 && IsTall(tex)) DrawTile(tex, origin + view.TileToScreen(c), Color.White);
            }
        }
        while (k < sorted.Count) DrawObject(sorted[k++], origin, selectedId, view, map.ShadowlessSprites);
    }

    private static void DrawObject(MapObject o, Vector2 origin, int? selectedId, IsoView view, IReadOnlySet<string> shadowless)
    {
        var rect = ObjectRect(o, origin, view);
        var tex = Sprites.Object(o.Sprite);
        // shadow at anchor (bottom-center, raised 2px to tuck under the base): faint outer + darker inner ellipse = soft edge
        if (o.Sprite != "spawn" && !(o.Sprite is { } s && shadowless.Contains(s)))
        {
            int cx = (int)(rect.X + rect.Width / 2), cy = (int)(rect.Y + rect.Height) - 2;
            Raylib.DrawEllipse(cx, cy, rect.Width * 0.4f, rect.Width * 0.2f, new Color(0, 0, 0, 40));
            Raylib.DrawEllipse(cx, cy, rect.Width * 0.28f, rect.Width * 0.14f, new Color(0, 0, 0, 50));
        }
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
    public static MapObject? ObjectAt(TileMap map, Vector2 origin, Vector2 p, IsoView view = default)
    {
        var hit = DrawOrder(map.Objects, view).LastOrDefault(o => Raylib.CheckCollisionPointRec(p, ObjectRect(o, origin, view)));
        if (hit is null) return null;
        float depth = view.Depth(hit.X, hit.Y);
        // full scan: runs once per click, not per frame. Objects behind hit are drawn before the same tiles, so covered too
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new TileCoord(x, y);
            var v = view.ToView(c);
            if (depth >= v.X + v.Y + 1) continue; // same test as DrawObjects: this tile is drawn before hit
            var tex = Sprites.Tile(map.Get(c));
            if (tex.Id != 0 && IsTall(tex) && BlockCovers(tex, origin + view.TileToScreen(c), p)) return null;
        }
        return hit;
    }

    private static readonly List<MapObject> Sorted = []; // reused: no per-frame allocation
    private static IsoView _sortView; // read by ByDepth: a capturing lambda would allocate every frame

    // Y-sort: larger x′+y′ = nearer the viewer = drawn later; ties by Id so the order is the same every frame
    private static readonly Comparison<MapObject> ByDepth = (a, b) =>
        _sortView.Depth(a.X, a.Y).CompareTo(_sortView.Depth(b.X, b.Y)) is var c and not 0 ? c : a.Id.CompareTo(b.Id);

    /// <summary>Shared buffer: valid until the next call.</summary>
    private static List<MapObject> DrawOrder(IReadOnlyList<MapObject> objects, IsoView view)
    {
        Sorted.Clear();
        Sorted.AddRange(objects);
        _sortView = view;
        Sorted.Sort(ByDepth);
        return Sorted;
    }
}
