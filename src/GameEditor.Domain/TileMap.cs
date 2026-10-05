namespace GameEditor.Domain;

public sealed class TileMap
{
    private readonly int[] _tiles;

    public int Width { get; }
    public int Height { get; }

    public TileMap(int width, int height) : this(width, height, new int[width * height]) { }

    public TileMap(int width, int height, int[] tiles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (width * height != tiles.Length)
            throw new ArgumentException($"Expected {width * height} tiles, got {tiles.Length}.", nameof(tiles));
        Width = width;
        Height = height;
        _tiles = [.. tiles];
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
}
