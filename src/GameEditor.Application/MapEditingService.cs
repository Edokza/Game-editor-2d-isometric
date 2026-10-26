using GameEditor.Domain;

namespace GameEditor.Application;

public readonly record struct Result(bool Success, string Message);

public sealed class MapEditingService(TileMap map, IMapRepository repository)
{
    /// <summary>Largest width/height for <see cref="New"/> and <see cref="Resize"/>.</summary>
    public const int MaxSize = 256;

    private readonly UndoStack _undo = new();
    private Dictionary<TileCoord, (int Old, int New)>? _stroke;
    private (int Index, MapObject Before, int Token)? _edit;
    private int _lastToken;
    private bool _dirty;

    public TileMap Map { get; private set; } = map;
    /// <summary>Name of the last successful save/load; null for a new map.</summary>
    public string? CurrentName { get; private set; }
    public bool CanUndo => _undo.CanUndo;
    public bool CanRedo => _undo.CanRedo;
    /// <summary>Edited since the last successful save/load, including an unfinished stroke or object edit.</summary>
    public bool IsDirty => _dirty || _stroke is { Count: > 0 } || _edit is { } e && Map.Objects[e.Index] != e.Before;

    public void BeginStroke()
    {
        Flush();
        _stroke = [];
    }

    /// <summary>Applies immediately; ignored outside a stroke or out of bounds.</summary>
    public void Paint(TileCoord c, int tileId)
    {
        if (_stroke is null || !Map.InBounds(c)) return;
        int current = Map.Get(c);
        if (current == tileId) return;
        _stroke[c] = (_stroke.TryGetValue(c, out var v) ? v.Old : current, tileId);
        Map.Set(c, tileId);
    }

    /// <summary>Whole stroke becomes one undo step.</summary>
    public void EndStroke()
    {
        if (_stroke is { Count: > 0 })
        {
            _undo.Push(new PaintTilesCommand(Map, _stroke));
            _dirty = true;
        }
        _stroke = null;
    }

    /// <summary>4-way flood fill of the region sharing <paramref name="c"/>'s tile id; one undo step.
    /// No-op outside the map or when already <paramref name="tileId"/>.</summary>
    public void Fill(TileCoord c, int tileId)
    {
        Flush();
        if (!Map.InBounds(c)) return;
        int from = Map.Get(c);
        if (from == tileId) return;
        var changes = new Dictionary<TileCoord, (int Old, int New)> { [c] = (from, tileId) };
        var queue = new Queue<TileCoord>([c]); // queue, not recursion: 256×256 would overflow the stack
        while (queue.TryDequeue(out var p))
            foreach (var n in (ReadOnlySpan<TileCoord>)[new(p.X + 1, p.Y), new(p.X - 1, p.Y), new(p.X, p.Y + 1), new(p.X, p.Y - 1)])
                if (Map.InBounds(n) && Map.Get(n) == from && changes.TryAdd(n, (from, tileId))) queue.Enqueue(n);
        Do(new PaintTilesCommand(Map, changes));
    }

    /// <summary>Appends at (<paramref name="x"/>, <paramref name="y"/>) tile space with a fresh id.
    /// A spawn when one exists moves that one instead: the hero has one spawn point.</summary>
    public MapObject AddObject(string name, float x, float y, string? sprite = null)
    {
        Flush();
        if (sprite == PlaySession.SpawnSprite && Map.Objects.FirstOrDefault(o => o.Sprite == sprite) is { } spawn)
        {
            var moved = spawn with { X = x, Y = y };
            if (moved != spawn) Do(new ObjectCommand(Map, Map.IndexOf(spawn.Id), spawn, moved));
            return moved;
        }
        var o = new MapObject(Map.NextObjectId, name, x, y, sprite);
        Do(new ObjectCommand(Map, Map.Objects.Count, null, o));
        return o;
    }

    /// <summary>One undo step; no-op when already that way.</summary>
    public void SetSolid(int tileId, bool solid)
    {
        Flush();
        if (Map.Solid.Contains(tileId) != solid) Do(new SolidCommand(Map, tileId, solid));
    }

    public bool RemoveObject(int id)
    {
        Flush();
        int i = Map.IndexOf(id);
        if (i < 0) return false;
        Do(new ObjectCommand(Map, i, Map.Objects[i], null));
        return true;
    }

    /// <summary>Starts a live edit (e.g. an Inspector drag); <see cref="EndObjectEdit(int)"/> makes it one undo step.</summary>
    /// <returns>Token for <see cref="EndObjectEdit(int)"/>; 0 when the id is absent.</returns>
    public int BeginObjectEdit(int id)
    {
        Flush();
        int i = Map.IndexOf(id);
        if (i < 0) return 0;
        _edit = (i, Map.Objects[i], ++_lastToken);
        return _lastToken;
    }

    /// <summary>Applies immediately; ignored outside an edit of that id or with a non-finite position.</summary>
    public void UpdateObject(MapObject o)
    {
        if (_edit is not { } e || o.Id != e.Before.Id || !float.IsFinite(o.X) || !float.IsFinite(o.Y)) return;
        Map.Replace(e.Index, o);
    }

    /// <summary>No-op when another Begin/undo/save already closed that edit, so one widget can't end another's edit.</summary>
    public void EndObjectEdit(int token)
    {
        if (_edit?.Token == token) EndObjectEdit();
    }

    private void EndObjectEdit()
    {
        if (_edit is { } e && Map.Objects[e.Index] is var now && now != e.Before)
        {
            _undo.Push(new ObjectCommand(Map, e.Index, e.Before, now));
            _dirty = true;
        }
        _edit = null;
    }

    public bool Undo()
    {
        Flush();
        if (!_undo.Undo()) return false;
        _dirty = true;
        return true;
    }

    public bool Redo()
    {
        Flush();
        if (!_undo.Redo()) return false;
        _dirty = true;
        return true;
    }

    /// <summary>Empty on error.</summary>
    public IReadOnlyList<string> ListMaps()
    {
        try { return repository.List(); }
        catch (Exception) { return []; }
    }

    public Result Save(string name)
    {
        Flush();
        try
        {
            repository.Save(Map, name);
            CurrentName = name;
            _dirty = false;
            return new(true, $"Saved '{name}'.");
        }
        catch (Exception e)
        {
            return new(false, $"Save failed: {e.Message}");
        }
    }

    /// <summary>Blank map with <see cref="TileMap.DefaultSolid"/>; drops any unsaved edits. Each side 1–<see cref="MaxSize"/>.</summary>
    public void New(int width, int height)
    {
        CheckSize(width, height);
        _stroke = null;
        _edit = null;
        Map = new TileMap(width, height);
        CurrentName = null;
        _undo.Clear();
        _dirty = false;
    }

    /// <summary>Keeps the top-left corner; new cells are 0, objects past the new right/bottom edge are dropped.
    /// One undo step: undo restores the previous <see cref="TileMap"/> instance.</summary>
    public void Resize(int width, int height)
    {
        CheckSize(width, height);
        if (width == Map.Width && height == Map.Height) return; // OK on an unchanged popup is not an edit
        Flush();
        var tiles = new int[width * height];
        for (int y = 0; y < Math.Min(height, Map.Height); y++)
            for (int x = 0; x < Math.Min(width, Map.Width); x++)
                tiles[y * width + x] = Map.Get(new TileCoord(x, y));
        Do(new ResizeCommand(m => Map = m, Map, new TileMap(width, height, tiles, Map.Objects.Where(o => o.X < width && o.Y < height), Map.Solid)));
    }

    public Result Load(string name)
    {
        Flush();
        try
        {
            Map = repository.Load(name);
            CurrentName = name;
            _undo.Clear();
            _dirty = false;
            return new(true, $"Loaded '{name}'.");
        }
        catch (Exception e)
        {
            return new(false, $"Load failed: {e.Message}");
        }
    }

    private static void CheckSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MaxSize);
    }

    private void Do(ICommand c)
    {
        c.Execute();
        _undo.Push(c);
        _dirty = true;
    }

    private void Flush()
    {
        EndStroke();
        EndObjectEdit();
    }
}
