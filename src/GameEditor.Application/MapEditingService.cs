using GameEditor.Domain;

namespace GameEditor.Application;

public readonly record struct Result(bool Success, string Message);

public sealed class MapEditingService(TileMap map, IMapRepository repository)
{
    private readonly UndoStack _undo = new();
    private Dictionary<TileCoord, (int Old, int New)>? _stroke;

    public TileMap Map { get; private set; } = map;
    /// <summary>Name of the last successful save/load; null for a new map.</summary>
    public string? CurrentName { get; private set; }
    public bool CanUndo => _undo.CanUndo;
    public bool CanRedo => _undo.CanRedo;

    public void BeginStroke()
    {
        EndStroke();
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
        if (_stroke is { Count: > 0 }) _undo.Push(new PaintTilesCommand(Map, _stroke));
        _stroke = null;
    }

    public bool Undo()
    {
        EndStroke();
        return _undo.Undo();
    }

    public bool Redo()
    {
        EndStroke();
        return _undo.Redo();
    }

    /// <summary>Empty on error.</summary>
    public IReadOnlyList<string> ListMaps()
    {
        try { return repository.List(); }
        catch (Exception) { return []; }
    }

    public Result Save(string name)
    {
        EndStroke();
        try
        {
            repository.Save(Map, name);
            CurrentName = name;
            return new(true, $"Saved '{name}'.");
        }
        catch (Exception e)
        {
            return new(false, $"Save failed: {e.Message}");
        }
    }

    public Result Load(string name)
    {
        EndStroke();
        try
        {
            Map = repository.Load(name);
            CurrentName = name;
            _undo.Clear();
            return new(true, $"Loaded '{name}'.");
        }
        catch (Exception e)
        {
            return new(false, $"Load failed: {e.Message}");
        }
    }
}
