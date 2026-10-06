using Raylib_cs;

namespace GameEditor.Rendering;

/// <summary>Textures from <c>{root}/tiles/&lt;id&gt;_*.png</c>, <c>{root}/objects/&lt;name&gt;.png</c> and <c>{root}/characters/&lt;name&gt;.png</c>.
/// Load after InitWindow, Unload before CloseWindow. Missing file → Id 0 → callers fall back to color/box.</summary>
public static class Sprites
{
    private const int MaxTileId = 1023; // "999999_x.png" must not allocate a huge array

    /// <summary>Index = tileId.</summary>
    public static Texture2D[] Tiles { get; private set; } = [];
    public static Dictionary<string, Texture2D> Objects { get; } = [];
    /// <summary>Not in <see cref="Objects"/>: the Assets panel must not offer them, but <see cref="Object"/> still finds them.</summary>
    public static Dictionary<string, Texture2D> Characters { get; } = [];

    public static Texture2D Tile(int id) => (uint)id < (uint)Tiles.Length ? Tiles[id] : default;

    public static Texture2D Object(string? name) =>
        name is not null && (Objects.TryGetValue(name, out var t) || Characters.TryGetValue(name, out t)) ? t : default;

    public static void Load(string root)
    {
        var tiles = new Texture2D[MaxTileId + 1];
        int count = 0;
        foreach (var f in Files(Path.Combine(root, "tiles")))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            int cut = name.IndexOf('_');
            if (!int.TryParse(cut < 0 ? name : name[..cut], out int id) || (uint)id > MaxTileId || tiles[id].Id != 0) continue;
            tiles[id] = LoadPng(f);
            count = Math.Max(count, id + 1);
        }
        Tiles = tiles[..count];

        LoadNamed(Path.Combine(root, "objects"), Objects);
        LoadNamed(Path.Combine(root, "characters"), Characters);
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
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return default; }
        var img = Raylib.LoadImageFromMemory(".png", bytes);
        if (img.Width == 0) return default; // corrupt or empty file; raylib already logged a warning
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        return tex;
    }

    public static void Unload()
    {
        foreach (var t in Tiles) if (t.Id != 0) Raylib.UnloadTexture(t);
        foreach (var t in Objects.Values.Concat(Characters.Values)) Raylib.UnloadTexture(t); // dicts only hold Id != 0
        Tiles = [];
        Objects.Clear();
        Characters.Clear();
    }

    // ordinal: duplicate tile ids resolve the same way on every machine/culture (first file wins)
    private static string[] Files(string dir) => Directory.Exists(dir) ? [.. Directory.GetFiles(dir, "*.png").Order(StringComparer.Ordinal)] : [];
}
