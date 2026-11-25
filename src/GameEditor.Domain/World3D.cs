using System.Numerics;

namespace GameEditor.Domain;

/// <summary>3D world: data cell (x, y) = box [x, x+1] × [y, y+1] on world X/Z, Y up, top at height × <see cref="Step"/>.
/// Yaw (degrees) = view turn; 90 × n matches the old 90° view rotation n (clockwise on screen).</summary>
public static class World3D
{
    /// <summary>World units per <see cref="TileMap"/> height step (a tile is 1 × 1).</summary>
    public const float Step = 0.5f;

    public static Vector3 At(float x, float y, float steps) => new(x, steps * Step, y);

    /// <summary>Screen-relative direction (W = (−1, −1) = away from the viewer at yaw 0) → data tile space.</summary>
    public static Vector2 DirFromScreen(Vector2 d, float yawDeg)
    {
        float a = -yawDeg * MathF.PI / 180, c = MathF.Cos(a), s = MathF.Sin(a);
        var r = new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
        // snap float noise: 90° turns must give exact axes, so the hero doesn't drift sideways
        return new(MathF.Round(r.X, 5), MathF.Round(r.Y, 5));
    }

    /// <summary>Horizontal unit vector from the look target toward the viewer (data x, y).</summary>
    public static Vector2 ToViewer(float yawDeg) => Vector2.Normalize(DirFromScreen(new(1, 1), yawDeg));

    /// <summary>Where the ray meets the plane Y = <paramref name="y"/>, as data (x, y); null when parallel or behind.</summary>
    public static Vector2? PlaneHit(Vector3 o, Vector3 d, float y)
    {
        if (MathF.Abs(d.Y) < 1e-6f) return null;
        float t = (y - o.Y) / d.Y;
        return t < 0 || !float.IsFinite(t) ? null : new(o.X + d.X * t, o.Z + d.Z * t);
    }

    /// <summary>First cell whose column (ground up to <paramref name="top"/>, world Y) the ray hits, walking the grid front to back.
    /// <paramref name="top"/> null = height × <see cref="Step"/>. T = ray parameter where it enters that column (distance for a unit
    /// <paramref name="d"/>). Null = misses the map.</summary>
    public static (TileCoord Cell, float T)? Pick(Vector3 o, Vector3 d, TileMap map, Func<TileCoord, float>? top = null)
    {
        top ??= c => map.GetHeight(c) * Step;
        float t0 = 0, t1 = float.MaxValue;
        if (!Slab(o.X, d.X, map.Width, ref t0, ref t1) || !Slab(o.Z, d.Z, map.Height, ref t0, ref t1)) return null;
        var p = o + d * t0;
        int x = Math.Clamp((int)MathF.Floor(p.X), 0, map.Width - 1), y = Math.Clamp((int)MathF.Floor(p.Z), 0, map.Height - 1);
        int sx = Math.Sign(d.X), sy = Math.Sign(d.Z);
        float dx = sx == 0 ? float.MaxValue : 1 / MathF.Abs(d.X), dy = sy == 0 ? float.MaxValue : 1 / MathF.Abs(d.Z);
        float tx = sx > 0 ? (x + 1 - o.X) / d.X : sx < 0 ? (x - o.X) / d.X : float.MaxValue;
        float ty = sy > 0 ? (y + 1 - o.Z) / d.Z : sy < 0 ? (y - o.Z) / d.Z : float.MaxValue;
        float t = t0;
        while (t <= t1 && (uint)x < (uint)map.Width && (uint)y < (uint)map.Height)
        {
            float tn = MathF.Min(MathF.Min(tx, ty), t1);
            var c = new TileCoord(x, y);
            // lowest point of the ray over this cell under the top = hit; T = where it crosses the top, or the side it enters by
            if (MathF.Min(o.Y + d.Y * t, o.Y + d.Y * tn) <= top(c))
                return (c, d.Y < 0 && o.Y + d.Y * t > top(c) ? (top(c) - o.Y) / d.Y : t);
            if (tx < ty) (x, t, tx) = (x + sx, tx, tx + dx);
            else (y, t, ty) = (y + sy, ty, ty + dy);
        }
        return null;
    }

    // ray range inside [0, max] on one axis
    private static bool Slab(float o, float d, float max, ref float t0, ref float t1)
    {
        if (d == 0) return o >= 0 && o <= max;
        float a = (0 - o) / d, b = (max - o) / d;
        if (a > b) (a, b) = (b, a);
        t0 = MathF.Max(t0, a);
        t1 = MathF.Min(t1, b);
        return t0 <= t1;
    }
}
