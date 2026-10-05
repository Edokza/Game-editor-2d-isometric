namespace GameEditor.Domain;

public sealed class TileMap
{
    private readonly int[] _tiles;
    private readonly List<MapObject> _objects = [];

    public int Width { get; }
    public int Height { get; }

    public TileMap(int width, int height) : this(width, height, new int[checked(width * height)]) { }

    public TileMap(int width, int height, int[] tiles, IEnumerable<MapObject>? objects = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        long count = (long)width * height; // int multiply can wrap to tiles.Length on corrupt files
        if (count != tiles.Length)
            throw new ArgumentException($"Expected {count} tiles, got {tiles.Length}.", nameof(tiles));
        Width = width;
        Height = height;
        _tiles = [.. tiles];
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
