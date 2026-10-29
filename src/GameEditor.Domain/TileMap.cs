namespace GameEditor.Domain;

public sealed class TileMap
{
    private readonly int[] _tiles;
    private readonly int[] _heights;
    private readonly List<MapObject> _objects = [];
    private readonly HashSet<int> _solid;
    private readonly HashSet<string> _solidSprites;
    private readonly HashSet<string> _shadowlessSprites;

    /// <summary>assets/tiles ids: 1 water, 6 dirt_block, 7 stone_block. Used when a map doesn't list its own.</summary>
    public static readonly IReadOnlyList<int> DefaultSolid = [1, 6, 7];

    /// <summary>assets/objects sprites whose tile the hero can't enter. Used when a map doesn't list its own.</summary>
    public static readonly IReadOnlyList<string> DefaultSolidSprites = ["crate", "rock", "tree"];

    /// <summary>assets/objects sprites drawn without a ground shadow (a box sits flush on its tile). Used when a map doesn't list its own.</summary>
    public static readonly IReadOnlyList<string> DefaultShadowlessSprites = ["crate"];

    /// <summary>Highest step a tile can be raised to.</summary>
    public const int MaxHeight = 8;

    public int Width { get; }
    public int Height { get; }

    public TileMap(int width, int height) : this(width, height, new int[checked(width * height)]) { }

    /// <param name="solid">Tile ids the hero can't enter; null → <see cref="DefaultSolid"/>. Copied.</param>
    /// <param name="solidSprites">Object sprites the hero can't walk into; null → <see cref="DefaultSolidSprites"/>. Copied.</param>
    /// <param name="shadowlessSprites">Object sprites drawn without a shadow; null → <see cref="DefaultShadowlessSprites"/>. Copied.</param>
    /// <param name="heights">Steps per tile, same layout as <paramref name="tiles"/>, each 0–<see cref="MaxHeight"/>; null → all 0. Copied.</param>
    public TileMap(int width, int height, int[] tiles, IEnumerable<MapObject>? objects = null, IEnumerable<int>? solid = null,
        IEnumerable<string>? solidSprites = null, IEnumerable<string>? shadowlessSprites = null, int[]? heights = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        long count = (long)width * height; // int multiply can wrap to tiles.Length on corrupt files
        if (count != tiles.Length)
            throw new ArgumentException($"Expected {count} tiles, got {tiles.Length}.", nameof(tiles));
        Width = width;
        Height = height;
        _tiles = [.. tiles];
        if (heights is not null && heights.Length != tiles.Length)
            throw new ArgumentException($"Expected {count} heights, got {heights.Length}.", nameof(heights));
        if (heights?.Any(h => (uint)h > MaxHeight) == true)
            throw new ArgumentException($"Heights must be 0–{MaxHeight}.", nameof(heights));
        _heights = heights is null ? new int[tiles.Length] : [.. heights];
        _solid = [.. solid ?? DefaultSolid];
        _solidSprites = [.. solidSprites ?? DefaultSolidSprites];
        _shadowlessSprites = [.. shadowlessSprites ?? DefaultShadowlessSprites];
        var ids = new HashSet<int>(); // not Insert: its IndexOf per object is O(n²) on big files
        foreach (var o in objects ?? [])
        {
            CheckFields(o);
            if (!ids.Add(o.Id)) throw new ArgumentException($"Duplicate object id {o.Id}.", nameof(objects));
            _objects.Add(o);
        }
    }

    public ReadOnlySpan<int> Tiles => _tiles;

    public bool InBounds(TileCoord c) => (uint)c.X < (uint)Width && (uint)c.Y < (uint)Height;

    /// <summary>Returns 0 (empty) when out of bounds.</summary>
    public int Get(TileCoord c) => InBounds(c) ? _tiles[c.Y * Width + c.X] : 0;

    /// <summary>No-op when out of bounds.</summary>
    internal void Set(TileCoord c, int tileId)
    {
        if (InBounds(c)) _tiles[c.Y * Width + c.X] = tileId;
    }

    public ReadOnlySpan<int> Heights => _heights;

    /// <summary>Steps; 0 when out of bounds.</summary>
    public int GetHeight(TileCoord c) => InBounds(c) ? _heights[c.Y * Width + c.X] : 0;

    /// <summary>Clamped to 0–<see cref="MaxHeight"/>; no-op when out of bounds.</summary>
    internal void SetHeight(TileCoord c, int height)
    {
        if (InBounds(c)) _heights[c.Y * Width + c.X] = Math.Clamp(height, 0, MaxHeight);
    }

    public IReadOnlySet<int> Solid => _solid;

    internal void SetSolid(int tileId, bool solid)
    {
        if (solid) _solid.Add(tileId);
        else _solid.Remove(tileId);
    }

    public IReadOnlySet<string> SolidSprites => _solidSprites;

    internal void SetSolidSprite(string sprite, bool solid)
    {
        if (solid) _solidSprites.Add(sprite);
        else _solidSprites.Remove(sprite);
    }

    public IReadOnlySet<string> ShadowlessSprites => _shadowlessSprites;

    internal void SetShadow(string sprite, bool shadow)
    {
        if (shadow) _shadowlessSprites.Remove(sprite);
        else _shadowlessSprites.Add(sprite);
    }

    /// <summary>Draw/list order.</summary>
    public IReadOnlyList<MapObject> Objects => _objects;

    public int NextObjectId
    {
        get
        {
            int max = _objects.Count == 0 ? 0 : _objects.Max(o => o.Id);
            if (max < int.MaxValue) return max + 1;
            // max + 1 would wrap to int.MinValue and repeat; take the lowest free positive id instead
            var used = _objects.Select(o => o.Id).ToHashSet();
            int id = 1;
            while (used.Contains(id)) id++;
            return id;
        }
    }

    /// <summary>-1 when absent.</summary>
    public int IndexOf(int id)
    {
        // plain loop: FindIndex with a capturing lambda allocates, and panels call this every frame
        for (int i = 0; i < _objects.Count; i++)
            if (_objects[i].Id == id) return i;
        return -1;
    }

    internal void Insert(int index, MapObject o)
    {
        Validate(o, -1);
        _objects.Insert(index, o);
    }

    internal void RemoveAt(int index) => _objects.RemoveAt(index);

    internal void Replace(int index, MapObject o)
    {
        Validate(o, index);
        _objects[index] = o;
    }

    private void Validate(MapObject o, int self)
    {
        CheckFields(o);
        int i = IndexOf(o.Id);
        if (i >= 0 && i != self) throw new ArgumentException($"Duplicate object id {o.Id}.", nameof(o));
    }

    private static void CheckFields(MapObject o)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(o.Name, nameof(o.Name));
        // NaN/∞ would break depth sort and drawing
        if (!float.IsFinite(o.X) || !float.IsFinite(o.Y))
            throw new ArgumentException($"Object {o.Id} has a non-finite position.", nameof(o));
    }
}
