using System.Numerics;
using GameEditor.Domain;

namespace GameEditor.Application;

/// <summary>Plays on a copy of the editor map, so Stop = drop the session; the source map is never touched.</summary>
public sealed class PlaySession
{
    /// <summary>Tiles per second.</summary>
    public const float Speed = 4f;
    private const float MaxDt = 0.1f; // a frame hitch must not step over a whole tile

    private readonly int _hero; // index in Map.Objects: appended last, nothing else inserts

    public TileMap Map { get; }
    public MapObject Hero => Map.Objects[_hero];

    public PlaySession(TileMap source)
    {
        Map = new TileMap(source.Width, source.Height, source.Tiles.ToArray(), source.Objects);
        _hero = Map.Objects.Count;
        Map.Insert(_hero, new MapObject(Map.NextObjectId, "Hero", Map.Width / 2f, Map.Height / 2f, "hero_se"));
    }

    /// <summary><paramref name="dir"/> in tile space, any length. One axis at a time so a blocked axis still lets the hero slide along the wall.</summary>
    public void Move(Vector2 dir, float dt)
    {
        var step = Vector2.Normalize(dir) * Speed * Math.Clamp(dt, 0, MaxDt);
        if (!float.IsFinite(step.X) || !float.IsFinite(step.Y)) return; // zero dir normalizes to NaN
        var h = Hero;
        float x = Blocked(h.X, h.Y, h.X + step.X, h.Y) ? h.X : h.X + step.X;
        float y = Blocked(x, h.Y, x, h.Y + step.Y) ? h.Y : h.Y + step.Y;
        // screen y grows with x + y, so a negative sum walks up the screen
        float down = step.X + step.Y;
        var sprite = down < 0 ? "hero_ne" : down > 0 ? "hero_se" : h.Sprite;
        Map.Replace(_hero, h with { X = x, Y = y, Sprite = sprite });
    }

    // only entering a new tile is checked: a hero spawned inside water can still walk out
    private bool Blocked(float fromX, float fromY, float toX, float toY)
    {
        var from = Tile(fromX, fromY);
        var to = Tile(toX, toY);
        return to != from && (!Map.InBounds(to) || Tiles.IsSolid(Map.Get(to)));
    }

    private static TileCoord Tile(float x, float y) => new((int)MathF.Floor(x), (int)MathF.Floor(y));
}
