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
    /// <summary>Per second, for <see cref="HeroZ"/>: 12 covers ~85% of a step in 0.15 s.</summary>
    private const float ClimbRate = 12f;

    private readonly int _hero; // index in Map.Objects: appended last, nothing else inserts
    private readonly HashSet<TileCoord> _objectBlocked; // objects don't move during play, so computed once

    public TileMap Map { get; }
    public MapObject Hero => Map.Objects[_hero];

    /// <summary>Drawn height of the hero in steps: eases toward the height of the tile it stands on, so a step up/down
    /// isn't a one-frame jump. Draw only; walking, depth sort and the shadow use the tile height.</summary>
    public float HeroZ { get; private set; }

    public PlaySession(TileMap source)
    {
        // spawn markers are editor-only: the first one inside the map places the hero (outside = stuck), none are drawn in play
        var spawn = source.Objects.FirstOrDefault(o => o.Sprite == SpawnSprite && source.InBounds(Tile(o.X, o.Y)));
        Map = new TileMap(source.Width, source.Height, source.Tiles.ToArray(),
            source.Objects.Where(o => o.Sprite != SpawnSprite), source.Solid, source.SolidSprites, source.ShadowlessSprites, source.Heights.ToArray());
        _objectBlocked = [.. Map.Objects.Where(o => o.Sprite is { } s && Map.SolidSprites.Contains(s)).Select(o => Tile(o.X, o.Y))];
        _hero = Map.Objects.Count;
        var (x, y) = spawn is null ? (Map.Width / 2f, Map.Height / 2f) : (spawn.X, spawn.Y);
        Map.Insert(_hero, new MapObject(Map.NextObjectId, "Hero", x, y, "hero_se"));
        HeroZ = Map.GetHeight(Tile(x, y));
    }

    /// <summary><paramref name="dir"/> in <paramref name="view"/> tile space (so WASD stays screen-relative after a rotation), any length;
    /// default view = map tile space. One axis at a time so a blocked axis still lets the hero slide along the wall.
    /// Call every frame, also with a zero <paramref name="dir"/>: it eases <see cref="HeroZ"/>.</summary>
    public void Move(Vector2 dir, float dt, IsoView view = default)
    {
        dt = float.IsFinite(dt) ? Math.Clamp(dt, 0, MaxDt) : 0;
        Walk(dir, dt, view);
        float target = Map.GetHeight(Tile(Hero.X, Hero.Y));
        HeroZ += (target - HeroZ) * Ease(ClimbRate, dt); // snap the tail so it really lands
        if (MathF.Abs(target - HeroZ) < 0.01f) HeroZ = target;
    }

    /// <summary>Fraction of the remaining gap to close this frame when easing at <paramref name="rate"/>/s: 1 − e^(−rate·dt),
    /// so the speed is the same at any fps and it never overshoots.</summary>
    public static float Ease(float rate, float dt) => 1 - MathF.Exp(-rate * MathF.Max(dt, 0));

    private void Walk(Vector2 dir, float dt, IsoView view)
    {
        var step = Vector2.Normalize(view.DirFromView(dir)) * Speed * dt;
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

    // only entering a new tile is checked: a hero spawned inside water or a tree can still walk out.
    // more than one step up or down is a wall
    private bool Blocked(float fromX, float fromY, float toX, float toY)
    {
        var from = Tile(fromX, fromY);
        var to = Tile(toX, toY);
        return to != from && (!Map.InBounds(to) || Map.Solid.Contains(Map.Get(to)) || _objectBlocked.Contains(to)
            || Math.Abs(Map.GetHeight(to) - Map.GetHeight(from)) > 1);
    }

    private static TileCoord Tile(float x, float y) => new((int)MathF.Floor(x), (int)MathF.Floor(y));
}
