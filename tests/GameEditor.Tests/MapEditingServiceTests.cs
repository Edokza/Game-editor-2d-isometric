using GameEditor.Application;
using GameEditor.Domain;

namespace GameEditor.Tests;

public class MapEditingServiceTests
{
    private sealed class FakeMapRepository : IMapRepository
    {
        public readonly Dictionary<string, TileMap> Stored = [];
        public bool Throw;

        public void Save(TileMap map, string name)
        {
            if (Throw) throw new IOException("disk full");
            Stored[name] = map;
        }

        public TileMap Load(string name) => Throw ? throw new IOException("bad file") : Stored[name];

        public IReadOnlyList<string> List() => Throw ? throw new IOException("no access") : [.. Stored.Keys];
    }

    [Fact]
    public void DragFiveCells_SingleUndoRevertsAll()
    {
        var svc = new MapEditingService(new TileMap(10, 10), new FakeMapRepository());
        svc.BeginStroke();
        for (int x = 0; x < 5; x++) svc.Paint(new TileCoord(x, 0), 3);
        svc.Paint(new TileCoord(2, 0), 4); // repaint same cell in stroke
        svc.EndStroke();

        Assert.Equal(4, svc.Map.Get(new TileCoord(2, 0)));
        Assert.True(svc.Undo());
        for (int x = 0; x < 5; x++) Assert.Equal(0, svc.Map.Get(new TileCoord(x, 0)));
        Assert.False(svc.CanUndo);

        Assert.True(svc.Redo());
        Assert.Equal(3, svc.Map.Get(new TileCoord(0, 0)));
        Assert.Equal(4, svc.Map.Get(new TileCoord(2, 0)));
    }

    [Fact]
    public void Load_ClearsUndo_SetsCurrentName()
    {
        var repo = new FakeMapRepository();
        repo.Stored["a"] = new TileMap(2, 2);
        var svc = new MapEditingService(new TileMap(2, 2), repo);
        svc.BeginStroke();
        svc.Paint(new TileCoord(0, 0), 1);
        svc.EndStroke();
        Assert.True(svc.CanUndo);

        Assert.True(svc.Load("a").Success);
        Assert.False(svc.CanUndo);
        Assert.Same(repo.Stored["a"], svc.Map);
        Assert.Equal("a", svc.CurrentName);
    }

    [Fact]
    public void Save_SetsCurrentName()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        Assert.Null(svc.CurrentName);
        Assert.True(svc.Save("b").Success);
        Assert.Equal("b", svc.CurrentName);
    }

    [Fact]
    public void IsDirty_SetByEditUndoRedo_ClearedBySaveAndLoad()
    {
        var repo = new FakeMapRepository();
        var svc = new MapEditingService(new TileMap(2, 2), repo);
        Assert.False(svc.IsDirty);

        svc.BeginStroke();
        svc.EndStroke(); // empty stroke is not an edit
        Assert.False(svc.IsDirty);

        svc.BeginStroke();
        svc.Paint(new TileCoord(0, 0), 1);
        Assert.True(svc.IsDirty); // mid-stroke counts
        svc.EndStroke();
        Assert.True(svc.IsDirty);
        Assert.True(svc.Save("a").Success);
        Assert.False(svc.IsDirty);

        Assert.True(svc.Undo());
        Assert.True(svc.IsDirty);
        Assert.True(svc.Load("a").Success);
        Assert.False(svc.IsDirty);
    }

    [Fact]
    public void New_BlankMapSameSize_ResetsState()
    {
        var repo = new FakeMapRepository();
        var svc = new MapEditingService(new TileMap(3, 2), repo);
        svc.BeginStroke();
        svc.Paint(new TileCoord(0, 0), 1);
        svc.EndStroke();
        Assert.True(svc.Save("a").Success);
        svc.BeginStroke();
        svc.Paint(new TileCoord(1, 0), 2);
        svc.EndStroke();
        Assert.True(svc.Undo()); // leaves something to redo

        svc.BeginStroke();
        svc.Paint(new TileCoord(2, 1), 5); // unfinished stroke is discarded, not pushed
        svc.New();

        Assert.Equal((3, 2), (svc.Map.Width, svc.Map.Height));
        Assert.All(svc.Map.Tiles.ToArray(), t => Assert.Equal(0, t));
        Assert.False(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.Null(svc.CurrentName);
        Assert.False(svc.IsDirty);
        Assert.Equal(1, repo.Stored["a"].Get(new TileCoord(0, 0))); // saved map untouched
    }

    [Fact]
    public void RepositoryThrows_ReturnsFailure()
    {
        var map = new TileMap(2, 2);
        var svc = new MapEditingService(map, new FakeMapRepository { Throw = true });

        var save = svc.Save("x");
        var load = svc.Load("x");

        Assert.False(save.Success);
        Assert.Contains("disk full", save.Message);
        Assert.False(load.Success);
        Assert.Same(map, svc.Map);
        Assert.Null(svc.CurrentName);
        Assert.Empty(svc.ListMaps());
    }
}
