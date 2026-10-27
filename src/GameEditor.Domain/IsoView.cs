using System.Numerics;

namespace GameEditor.Domain;

/// <summary>The map seen from one of four sides, in 90° steps. View only: map data stays in (x, y), drawing and picking
/// go through view coords (x′, y′). <paramref name="width"/>/<paramref name="height"/> = map size.
/// default = rotation 0 = plain <see cref="IsoMath"/>, and rotation 0 never reads the size.</summary>
public readonly struct IsoView(int rotation, int width, int height)
{
    private static readonly TileCoord Invalid = new(int.MinValue, int.MinValue);

    /// <summary>0–3; any int is wrapped (5 → 1, −1 → 3).</summary>
    public int Rotation { get; } = (rotation % 4 + 4) % 4;
    public int Width { get; } = width;
    public int Height { get; } = height;

    /// <summary>Map size as drawn: odd rotations swap the sides.</summary>
    public int ViewWidth => (Rotation & 1) == 0 ? Width : Height;
    public int ViewHeight => (Rotation & 1) == 0 ? Height : Width;

    public IsoView Rotated(int steps) => new(Rotation + steps, Width, Height);

    public TileCoord ToView(TileCoord c) => Rotation switch
    {
        1 => new(Height - 1 - c.Y, c.X),
        2 => new(Width - 1 - c.X, Height - 1 - c.Y),
        3 => new(c.Y, Width - 1 - c.X),
        _ => c,
    };

    public TileCoord FromView(TileCoord v) => Rotation switch
    {
        1 => new(v.Y, Height - 1 - v.X),
        2 => new(Width - 1 - v.X, Height - 1 - v.Y),
        3 => new(Width - 1 - v.Y, v.X),
        _ => v,
    };

    /// <summary>Fractional tile-space point (e.g. an object position): cell (x, y) spans [x, x+1), so a point inside a cell
    /// lands inside that cell's <see cref="ToView(TileCoord)"/>. No -1: that's the cell's far edge.</summary>
    public Vector2 ToView(float x, float y) => Rotation switch
    {
        1 => new(Height - y, x),
        2 => new(Width - x, Height - y),
        3 => new(y, Width - x),
        _ => new(x, y),
    };

    public Vector2 FromView(Vector2 v) => Rotation switch
    {
        1 => new(v.Y, Height - v.X),
        2 => new(Width - v.X, Height - v.Y),
        3 => new(Width - v.Y, v.X),
        _ => v,
    };

    /// <summary>Screen point of data cell <paramref name="c"/>'s top vertex, as drawn in this view.</summary>
    public Vector2 TileToScreen(TileCoord c) => IsoMath.TileToScreen(ToView(c));

    public Vector2 TileToScreen(float x, float y)
    {
        var v = ToView(x, y);
        return IsoMath.TileToScreen(v.X, v.Y);
    }

    /// <summary>Data cell under a screen point. Non-finite input keeps <see cref="IsoMath.ScreenToTile"/>'s out-of-bounds sentinel
    /// (rotating it would wrap around int).</summary>
    public TileCoord ScreenToTile(Vector2 p) => IsoMath.ScreenToTile(p) is var v && v == Invalid ? v : FromView(v);

    public Vector2 ScreenToTileF(Vector2 p) => FromView(IsoMath.ScreenToTileF(p));

    /// <summary>Draw order key of a tile-space point: x′ + y′, larger = nearer the viewer = drawn later.</summary>
    public float Depth(float x, float y)
    {
        var v = ToView(x, y);
        return v.X + v.Y;
    }
}
