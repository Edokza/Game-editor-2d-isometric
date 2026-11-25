using System.Numerics;
using Raylib_cs;

namespace GameEditor.Rendering;

/// <summary>Textures from <c>{root}/tiles/&lt;id&gt;_*.png</c>, <c>{root}/objects/&lt;name&gt;.png</c> and <c>{root}/characters/&lt;name&gt;.png</c>.
/// Load after InitWindow, Unload before CloseWindow. Missing file → Id 0 → callers fall back to color/box.</summary>
public static class Sprites
{
    private const int MaxTileId = 1023; // "999999_x.png" must not allocate a huge array

    /// <summary>Index = tileId.</summary>
    public static Texture2D[] Tiles { get; private set; } = [];
    /// <summary>Square top faces of 3D tile boxes from <c>{root}/tiles_top/</c> in one texture, so the whole terrain is one draw per
    /// chunk. Faces sit in a grid in load order (not by id: a high id must not make it wider than the GPU allows), each resized to the
    /// largest face and edge-extruded 1px so distant sampling never bleeds into a neighbour. The cell after the last face is white:
    /// the mesh tints it with the palette color for ids without an image.</summary>
    public static Texture2D TopAtlas { get; private set; }
    private static Dictionary<int, int> _topSlot = []; // tile id → cell
    private static int _cols = 1, _rows = 1, _topSize = 1;

    public static Dictionary<string, Texture2D> Objects { get; } = [];
    /// <summary>Not in <see cref="Objects"/>: the Assets panel must not offer them, but <see cref="Object"/> still finds them.</summary>
    public static Dictionary<string, Texture2D> Characters { get; } = [];

    public static bool HasTop(int id) => _topSlot.ContainsKey(id);

    public static Texture2D Tile(int id) => (uint)id < (uint)Tiles.Length ? Tiles[id] : default;
    /// <summary>UV rect (u0, v0, u1, v1) of a tile's top face; ids without an image get the white fallback cell.</summary>
    public static Vector4 TileTopUv(int id) => AtlasUv(_topSlot.TryGetValue(id, out int s) ? s : _topSlot.Count, _cols, _rows, _topSize);

    /// <summary>Inner (unextruded) rect of cell <paramref name="slot"/> in a <paramref name="cols"/> × <paramref name="rows"/> grid of
    /// (size + 2) px cells, filled row by row.</summary>
    public static Vector4 AtlasUv(int slot, int cols, int rows, int size)
    {
        float cell = size + 2f, w = cols * cell, h = rows * cell, x = slot % cols * cell + 1, y = slot / cols * cell + 1;
        return new(x / w, y / h, (x + size) / w, (y + size) / h);
    }

    public static Texture2D Object(string? name) =>
        name is not null && (Objects.TryGetValue(name, out var t) || Characters.TryGetValue(name, out t)) ? t : default;

    public static void Load(string root)
    {
        Tiles = LoadIds(Path.Combine(root, "tiles"));
        LoadTopAtlas(Path.Combine(root, "tiles_top"));
        LoadNamed(Path.Combine(root, "objects"), Objects);
        LoadNamed(Path.Combine(root, "characters"), Characters);
    }

    private static Texture2D[] LoadIds(string dir)
    {
        var tiles = new Texture2D[MaxTileId + 1];
        int count = 0;
        foreach (var f in Files(dir))
        {
            if (Id(f) is not { } id || tiles[id].Id != 0) continue;
            tiles[id] = LoadPng(f);
            count = Math.Max(count, id + 1);
        }
        return tiles[..count];
    }

    private static void LoadTopAtlas(string dir)
    {
        var faces = new SortedDictionary<int, Image>(); // id order: same layout every run
        foreach (var f in Files(dir))
            if (Id(f) is { } id && !faces.ContainsKey(id) && LoadImage(f) is { Width: > 0 } img) faces[id] = img;
        int size = faces.Count == 0 ? 1 : faces.Values.Max(i => Math.Max(i.Width, i.Height));
        int cells = faces.Count + 1, cols = (int)MathF.Ceiling(MathF.Sqrt(cells)), rows = (cells + cols - 1) / cols, cell = size + 2;
        // ponytail: 1024 ids × 66 px fits 2.2k square; huge faces (> ~500 px each) could still pass the GPU limit, cap size if that shows
        var atlas = Raylib.GenImageColor(cols * cell, rows * cell, Color.White);
        var slots = new Dictionary<int, int>();
        foreach (var (id, face) in faces)
        {
            var img = face;
            // mixed sizes: scale to one cell, so every copy below is whole pixels (a sub-pixel source rect divides by zero in raylib)
            if (img.Width != size || img.Height != size) Raylib.ImageResizeNN(ref img, size, size);
            int slot = slots.Count, x = slot % cols * cell + 1, y = slot / cols * cell + 1;
            slots[id] = slot;
            Raylib.ImageDraw(ref atlas, img, new(0, 0, size, size), new(x, y, size, size), Color.White);
            // extrude: copy each edge row/column one pixel outward
            Raylib.ImageDraw(ref atlas, img, new(0, 0, size, 1), new(x, y - 1, size, 1), Color.White);
            Raylib.ImageDraw(ref atlas, img, new(0, size - 1, size, 1), new(x, y + size, size, 1), Color.White);
            Raylib.ImageDraw(ref atlas, img, new(0, 0, 1, size), new(x - 1, y, 1, size), Color.White);
            Raylib.ImageDraw(ref atlas, img, new(size - 1, 0, 1, size), new(x + size, y, 1, size), Color.White);
            Raylib.UnloadImage(img);
        }
        TopAtlas = Raylib.LoadTextureFromImage(atlas);
        Raylib.UnloadImage(atlas);
        (_topSlot, _cols, _rows, _topSize) = (slots, cols, rows, size);
    }

    private static int? Id(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        int cut = name.IndexOf('_');
        return int.TryParse(cut < 0 ? name : name[..cut], out int id) && (uint)id <= MaxTileId ? id : null;
    }

    private static void LoadNamed(string dir, Dictionary<string, Texture2D> into)
    {
        foreach (var f in Files(dir))
            if (LoadPng(f) is { Id: not 0 } t && !into.TryAdd(Path.GetFileNameWithoutExtension(f), t))
                Raylib.UnloadTexture(t);
    }

    /// <summary>Id 0 on failure. Not Raylib.LoadTexture: its fopen reads the path as ANSI on Windows, so Thai/emoji paths fail.</summary>
    private static Texture2D LoadPng(string path)
    {
        var img = LoadImage(path);
        if (img.Width == 0) return default; // corrupt or empty file; raylib already logged a warning
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        return tex;
    }

    private static Image LoadImage(string path)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return default; }
        return Raylib.LoadImageFromMemory(".png", bytes);
    }

    public static void Unload()
    {
        foreach (var t in Tiles) if (t.Id != 0) Raylib.UnloadTexture(t);
        if (TopAtlas.Id != 0) Raylib.UnloadTexture(TopAtlas);
        TopAtlas = default;
        (_topSlot, _cols, _rows, _topSize) = ([], 1, 1, 1);
        foreach (var t in Objects.Values.Concat(Characters.Values)) Raylib.UnloadTexture(t); // dicts only hold Id != 0
        Tiles = [];
        Objects.Clear();
        Characters.Clear();
    }

    // ordinal: duplicate tile ids resolve the same way on every machine/culture (first file wins)
    private static string[] Files(string dir) => Directory.Exists(dir) ? [.. Directory.GetFiles(dir, "*.png").Order(StringComparer.Ordinal)] : [];
}
