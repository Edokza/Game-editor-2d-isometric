using System.Numerics;
using System.Runtime.InteropServices;
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

    /// <summary>Ground-level tiles only; tall ones (blocks) and raised ones are depth-sorted with objects in <see cref="DrawObjects"/>.
    /// <paramref name="origin"/> = screen position of view tile (0,0) top vertex. <paramref name="view"/> default = unrotated.</summary>
    public static void Draw(TileMap map, Vector2 origin, IsoView view = default)
    {
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new TileCoord(x, y);
            if (map.GetHeight(c) == 0) DrawGround(map, c, origin, view);
        }
    }

    /// <summary>One tile at its height: front walls, then the face (not a tall tile's image: <see cref="DrawObjects"/> draws that).</summary>
    private static void DrawGround(TileMap map, TileCoord c, Vector2 origin, IsoView view)
    {
        var right = new Vector2(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var left = new Vector2(-right.X, right.Y);
        var down = new Vector2(0, IsoMath.TileHeight);

        var v = view.ToView(c); // walls and ripple phase follow the screen, not the data axes
        int h = map.GetHeight(c);
        var top = origin + IsoMath.TileToScreen(v) + IsoMath.Lift(h);
        int id = map.Get(c);
        var tex = Sprites.Tile(id);
        // water ripple: brightness 0.7–1.0, phase (x′ + y′) → waves run diagonally; subtler was invisible on pastel tiles
        float shade = id == 1 ? -0.15f * (1 + MathF.Sin((float)Raylib.GetTime() * 3 + (v.X + v.Y) * 1.5f)) : 0;
        // front walls down to the neighbour's top; past the map edge one step below ground: map reads as a floating island
        var wallColor = (uint)id < (uint)Palette.Length ? Palette[id] : Palette[4];
        if (h - HeightAt(map, view, v.X, v.Y + 1) is var dl and > 0)
        {
            var wall = -IsoMath.Lift(dl);
            var col = Raylib.ColorBrightness(wallColor, -0.3f);
            Raylib.DrawTriangle(top + left, top + left + wall, top + down + wall, col);
            Raylib.DrawTriangle(top + left, top + down + wall, top + down, col);
        }
        if (h - HeightAt(map, view, v.X + 1, v.Y) is var dr and > 0)
        {
            var wall = -IsoMath.Lift(dr);
            var col = Raylib.ColorBrightness(wallColor, -0.5f);
            Raylib.DrawTriangle(top + down, top + down + wall, top + right + wall, col);
            Raylib.DrawTriangle(top + down, top + right + wall, top + right, col);
        }
        if (tex.Id != 0)
        {
            if (!IsTall(tex)) DrawTile(tex, top, Raylib.ColorBrightness(Color.White, shade));
            if (id == 1) DrawRipple(top, v.X + v.Y);
            return;
        }
        var color = Raylib.ColorBrightness(ColorOf(id), shade);
        Raylib.DrawTriangle(top, top + left, top + down, color);
        Raylib.DrawTriangle(top, top + down, top + right, color);
        Raylib.DrawLineV(top, top + right, Outline);
        Raylib.DrawLineV(top, top + left, Outline);
        if (id == 1) DrawRipple(top, v.X + v.Y);
    }

    /// <summary>Height of view cell (<paramref name="vx"/>, <paramref name="vy"/>); −1 past the map edge.</summary>
    private static int HeightAt(TileMap map, IsoView view, int vx, int vy) =>
        view.FromView(new TileCoord(vx, vy)) is var c && map.InBounds(c) ? map.GetHeight(c) : -1;

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

    /// <summary><paramref name="c"/> = data cell, outlined at <paramref name="height"/> steps.</summary>
    public static void DrawHighlight(TileCoord c, Vector2 origin, Color color, IsoView view = default, int height = 0)
    {
        var top = origin + view.TileToScreen(c) + IsoMath.Lift(height);
        var right = top + new Vector2(IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var left = top + new Vector2(-IsoMath.TileWidth / 2f, IsoMath.TileHeight / 2f);
        var bottom = top + new Vector2(0, IsoMath.TileHeight);
        Raylib.DrawLineV(top, right, color);
        Raylib.DrawLineV(right, bottom, color);
        Raylib.DrawLineV(bottom, left, color);
        Raylib.DrawLineV(left, top, color);
    }

    /// <summary>Sprite bounds (48×80 placeholder box without one); bottom center = object position raised <paramref name="z"/> steps.</summary>
    public static Rectangle ObjectRect(MapObject o, Vector2 origin, IsoView view = default, float z = 0)
    {
        var tex = Sprites.Object(o.Sprite);
        float w = tex.Id != 0 ? tex.Width : 48, h = tex.Id != 0 ? tex.Height : 80;
        var foot = origin + view.TileToScreen(o.X, o.Y) + IsoMath.Lift(z);
        // floor: odd widths and fractional positions would land texels between screen pixels
        return new Rectangle(MathF.Floor(foot.X - w / 2), MathF.Floor(foot.Y - h), w, h);
    }

    private static bool IsTall(Texture2D tex) => tex.Height > IsoMath.TileHeight;

    // image bottom center = tile bottom vertex
    private static void DrawTile(Texture2D tex, Vector2 top, Color tint) =>
        Raylib.DrawTextureV(tex, top + new Vector2(-tex.Width / 2f, IsoMath.TileHeight - tex.Height), tint);

    /// <summary>Pixel height of a tile's silhouette when <see cref="DrawObjects"/> draws it over objects (tall or raised), else 0.</summary>
    private static float Silhouette(TileMap map, TileCoord c)
    {
        var tex = Sprites.Tile(map.Get(c));
        bool tall = tex.Id != 0 && IsTall(tex);
        int h = map.GetHeight(c);
        return !tall && h == 0 ? 0 : (tall ? tex.Height : IsoMath.TileHeight) + h * IsoMath.StepHeight;
    }

    /// <summary>Silhouette = ground diamond at <paramref name="top"/> swept up to <paramref name="height"/> px; not an image rect,
    /// whose transparent corners show what is behind.</summary>
    private static bool Covers(float height, Vector2 top, Vector2 p)
    {
        float dx = MathF.Abs(p.X - top.X), slope = dx * IsoMath.TileHeight / IsoMath.TileWidth;
        float bottom = top.Y + IsoMath.TileHeight;
        return dx <= IsoMath.TileWidth / 2f && p.Y >= bottom - height + slope && p.Y <= bottom - slope;
    }

    private static bool FrontIsHigher(TileMap map, IsoView view, MapObject o)
    {
        var c = new TileCoord((int)MathF.Floor(o.X), (int)MathF.Floor(o.Y));
        var v = view.ToView(c);
        int h = map.GetHeight(c);
        return Above(v.X + 1, v.Y) || Above(v.X, v.Y + 1);

        bool Above(int vx, int vy) =>
            view.FromView(new TileCoord(vx, vy)) is var n && map.InBounds(n)
            && (map.GetHeight(n) > h || map.GetHeight(n) == h && IsTall(Sprites.Tile(map.Get(n))));
    }

    private static float GroundZ(TileMap map, MapObject o) => map.GetHeight(new TileCoord((int)MathF.Floor(o.X), (int)MathF.Floor(o.Y)));

    /// <summary>Objects, raised and tall tiles back to front, after <see cref="Draw"/>. Objects stand on their tile's height;
    /// <paramref name="lift"/> overrides one object's drawn height (the hero easing up a step), not its shadow or draw order.</summary>
    public static void DrawObjects(TileMap map, Vector2 origin, int? selectedId, IsoView view = default, (int Id, float Z)? lift = null)
    {
        var sorted = DrawOrder(map, view);
        int k = 0;
        // from map, not view.ViewWidth: a default view has no size
        bool odd = (view.Rotation & 1) != 0;
        int w = odd ? map.Height : map.Width, h = odd ? map.Width : map.Height;
        // view diagonal s = x′ + y′: tiles on one diagonal never overlap each other; objects in depth order, each once its Keys[k] diagonal is drawn
        for (int s = 0; s <= w + h - 2; s++)
        {
            while (k < sorted.Count && Keys[k] < s) DrawObject(map, sorted[k++], origin, selectedId, view, lift);
            for (int x = Math.Max(0, s - h + 1); x <= Math.Min(s, w - 1); x++)
            {
                var c = view.FromView(new TileCoord(x, s - x));
                int z = map.GetHeight(c);
                if (z > 0) DrawGround(map, c, origin, view);
                var tex = Sprites.Tile(map.Get(c));
                if (tex.Id != 0 && IsTall(tex)) DrawTile(tex, origin + view.TileToScreen(c) + IsoMath.Lift(z), Color.White);
            }
        }
        while (k < sorted.Count) DrawObject(map, sorted[k++], origin, selectedId, view, lift);
    }

    private static void DrawObject(TileMap map, MapObject o, Vector2 origin, int? selectedId, IsoView view, (int Id, float Z)? lift)
    {
        float ground = GroundZ(map, o);
        var rect = ObjectRect(o, origin, view, lift is { } l && l.Id == o.Id ? l.Z : ground);
        var tex = Sprites.Object(o.Sprite);
        // shadow on the ground under the anchor (raised 2px to tuck under the base): faint outer + darker inner ellipse = soft edge
        if (o.Sprite != "spawn" && !(o.Sprite is { } s && map.ShadowlessSprites.Contains(s)))
        {
            var foot = origin + view.TileToScreen(o.X, o.Y) + IsoMath.Lift(ground);
            int cx = (int)(rect.X + rect.Width / 2), cy = (int)MathF.Floor(foot.Y) - 2;
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
    /// or a tall/raised tile <see cref="DrawObjects"/> draws later covers it, so a click on a block paints instead of grabbing what it hides.</summary>
    public static MapObject? ObjectAt(TileMap map, Vector2 origin, Vector2 p, IsoView view = default)
    {
        var sorted = DrawOrder(map, view);
        int i = sorted.FindLastIndex(o => Raylib.CheckCollisionPointRec(p, ObjectRect(o, origin, view, GroundZ(map, o))));
        if (i < 0) return null;
        var hit = sorted[i];
        int depth = Keys[i];
        // full scan: runs once per click, not per frame. Objects behind hit are drawn before the same tiles, so covered too
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var c = new TileCoord(x, y);
            var v = view.ToView(c);
            if (v.X + v.Y <= depth) continue; // same test as DrawObjects: this tile is drawn before hit
            if (Silhouette(map, c) is var height and > 0 && Covers(height, origin + view.TileToScreen(c), p)) return null;
        }
        return hit;
    }

    private static readonly List<MapObject> Sorted = []; // reused: no per-frame allocation
    private static IsoView _sortView; // read by ByDepth: a capturing lambda would allocate every frame
    /// <summary>Per <see cref="Sorted"/> entry: the view diagonal after whose tiles it is drawn. Wanted: one past its cell's, so level or
    /// lower tiles in front don't cut off the sprite hanging over them; its own cell's when a higher or tall tile in front must cover it.
    /// Taken as the minimum over it and everything after it, so drawing keeps depth order, but never before its own cell's diagonal
    /// (that tile would cover what stands on it). Non-decreasing: the draw loop gates on it in order.</summary>
    private static readonly List<int> Keys = [];

    // Y-sort: larger x′+y′ = nearer the viewer = drawn later; ties by Id so the order is the same every frame
    private static readonly Comparison<MapObject> ByDepth = (a, b) =>
        _sortView.Depth(a.X, a.Y).CompareTo(_sortView.Depth(b.X, b.Y)) is var c and not 0 ? c : a.Id.CompareTo(b.Id);

    /// <summary>Shared buffers (and <see cref="Keys"/>): valid until the next call.</summary>
    private static List<MapObject> DrawOrder(TileMap map, IsoView view)
    {
        Sorted.Clear();
        Sorted.AddRange(map.Objects);
        _sortView = view;
        Sorted.Sort(ByDepth);
        CollectionsMarshal.SetCount(Keys, Sorted.Count); // every entry is written below
        int min = int.MaxValue;
        for (int i = Sorted.Count - 1; i >= 0; i--)
        {
            int d = view.CellDepth(Sorted[i].X, Sorted[i].Y);
            min = Math.Min(min, FrontIsHigher(map, view, Sorted[i]) ? d : d + 1);
            Keys[i] = Math.Max(min, d);
        }
        // the Max can leave a key above a later one (a covered object just behind it in depth): stable-sort by key so the draw loop
        // and ObjectAt see the same order. ponytail: that pair then draws out of depth order; insertion sort, nearly sorted so ~O(n)
        for (int i = 1; i < Keys.Count; i++)
            for (int j = i; j > 0 && Keys[j - 1] > Keys[j]; j--)
            {
                (Keys[j - 1], Keys[j]) = (Keys[j], Keys[j - 1]);
                (Sorted[j - 1], Sorted[j]) = (Sorted[j], Sorted[j - 1]);
            }
        return Sorted;
    }
}
