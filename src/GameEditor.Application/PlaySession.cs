using System.Numerics;
using GameEditor.Domain;

namespace GameEditor.Application;

/// <summary>Plays on a copy of the editor map, so Stop = drop the session; the source map is never touched.</summary>
public sealed class PlaySession
{
    /// <summary>Tiles per second.</summary>
    public const float Speed = 4f;
    public const string SpawnSprite = "spawn";
    private const float MaxDt = 0.1f; // a frame hitch must not step over a whole tile

    private readonly int _hero; // index in Map.Objects: appended last, nothing else inserts
    private readonly HashSet<TileCoord> _objectBlocked; // objects don't move during play, so computed once

    public TileMap Map { get; }
    public MapObject Hero => Map.Objects[_hero];

    public PlaySession(TileMap source)
    {
        // spawn markers are editor-only: the first one inside the map places the hero (outside = stuck), none are drawn in play
        var spawn = source.Objects.FirstOrDefault(o => o.Sprite == SpawnSprite && source.InBounds(Tile(o.X, o.Y)));
        Map = new TileMap(source.Width, source.Height, source.Tiles.ToArray(),
            source.Objects.Where(o => o.Sprite != SpawnSprite), source.Solid, source.SolidSprites);
        _objectBlocked = [.. Map.Objects.Where(o => o.Sprite is { } s && Map.SolidSprites.Contains(s)).Select(o => Tile(o.X, o.Y))];
        _hero = Map.Objects.Count;
        var (x, y) = spawn is null ? (Map.Width / 2f, Map.Height / 2f) : (spawn.X, spawn.Y);
        Map.Insert(_hero, new MapObject(Map.NextObjectId, "Hero", x, y, "hero_se"));
    }

    /// <summary><paramref name="dir"/> in <paramref name="view"/> tile space (so WASD stays screen-relative after a rotation), any length;
    /// default view = map tile space. One axis at a time so a blocked axis still lets the hero slide along the wall.</summary>
    public void Move(Vector2 dir, float dt, IsoView view = default)
    {
        var step = Vector2.Normalize(view.DirFromView(dir)) * Speed * Math.Clamp(dt, 0, MaxDt);
        if (!float.IsFinite(step.X) || !float.IsFinite(step.Y)) return; // zero dir normalizes to NaN
        var h = Hero;
        float x = Blocked(h.X, h.Y, h.X + step.X, h.Y) ? h.X : h.X + step.X;
        float y = Blocked(x, h.Y, x, h.Y + step.Y) ? h.Y : h.Y + step.Y;
        // sprites face screen directions, so use the view direction: screen y grows with x′ + y′, screen x with x′ − y′;
        // pure left/right (A/D) picks sw/ne, pure up/down (W/S) keeps the side
        float down = dir.X + dir.Y, right = dir.X - dir.Y;
        bool south = down != 0 ? down > 0 : right < 0;
        bool east = right != 0 ? right > 0 : h.Sprite?.EndsWith('w') != true;
        Map.Replace(_hero, h with { X = x, Y = y, Sprite = $"hero_{(south ? 's' : 'n')}{(east ? 'e' : 'w')}" });
    }

    // only entering a new tile is checked: a hero spawned inside water or a tree can still walk out
    private bool Blocked(float fromX, float fromY, float toX, float toY)
    {
        var from = Tile(fromX, fromY);
        var to = Tile(toX, toY);
        return to != from && (!Map.InBounds(to) || Map.Solid.Contains(Map.Get(to)) || _objectBlocked.Contains(to));
    }

    private static TileCoord Tile(float x, float y) => new((int)MathF.Floor(x), (int)MathF.Floor(y));
}
