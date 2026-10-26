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
        svc.New(3, 2);

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

    [Fact]
    public void AddSecondSpawn_MovesFirstInOneUndo()
    {
        var tree = new MapObject(1, "Tree", 1, 1, "tree");
        var svc = new MapEditingService(new TileMap(4, 4, new int[16], [tree]), new FakeMapRepository());
        var a = svc.AddObject("spawn", 0.5f, 0.5f, "spawn");
        var b = svc.AddObject("spawn", 2.5f, 3.5f, "spawn");

        Assert.Equal(a with { X = 2.5f, Y = 3.5f }, b);
        Assert.Equal([tree, b], svc.Map.Objects);
        Assert.True(svc.Undo());
        Assert.Equal([tree, a], svc.Map.Objects);
        Assert.True(svc.Undo());
        Assert.Equal([tree], svc.Map.Objects);
    }

    [Fact]
    public void AddRemoveObject_UndoRedo()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        var a = svc.AddObject("a", 0.5f, 0.5f);
        var b = svc.AddObject("b", 1.5f, 1.5f);
        var c = svc.AddObject("c", 1, 1);
        Assert.Equal([1, 2, 3], svc.Map.Objects.Select(o => o.Id));
        Assert.True(svc.IsDirty);

        Assert.True(svc.RemoveObject(b.Id));
        Assert.False(svc.RemoveObject(99));
        Assert.Equal([a, c], svc.Map.Objects);

        Assert.True(svc.Undo()); // back at its old index, not appended
        Assert.Equal([a, b, c], svc.Map.Objects);
        Assert.True(svc.Undo());
        Assert.Equal([a, b], svc.Map.Objects);
        Assert.True(svc.Redo());
        Assert.True(svc.Redo());
        Assert.Equal([a, c], svc.Map.Objects);
    }

    [Fact]
    public void ObjectEdit_ManyUpdates_SingleUndo()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        var a = svc.AddObject("a", 0, 0);
        Assert.True(svc.Save("s").Success);

        int token = svc.BeginObjectEdit(a.Id);
        Assert.NotEqual(0, token);
        for (int i = 1; i <= 5; i++) svc.UpdateObject(a with { X = i });
        Assert.Equal(5, svc.Map.Objects[0].X); // live
        Assert.True(svc.IsDirty); // mid-edit counts
        svc.UpdateObject(a with { Id = 42, X = 9 }); // other id ignored
        svc.UpdateObject(a with { Y = float.NaN });  // non-finite ignored
        Assert.Equal(a with { X = 5 }, svc.Map.Objects[0]);
        svc.EndObjectEdit(token);

        Assert.True(svc.Undo());
        Assert.Equal(a, svc.Map.Objects[0]); // one step back to before the drag
        Assert.True(svc.Redo());
        Assert.Equal(a with { X = 5 }, svc.Map.Objects[0]);
    }

    [Fact]
    public void ObjectEdit_NoChange_NotPushed_UpdateOutsideEditIgnored()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        var a = svc.AddObject("a", 0, 0);
        Assert.True(svc.Save("s").Success);

        svc.UpdateObject(a with { X = 3 }); // no edit open
        Assert.Equal(a, svc.Map.Objects[0]);

        int token = svc.BeginObjectEdit(a.Id);
        svc.UpdateObject(a with { X = 3 });
        svc.UpdateObject(a); // dragged back to start
        svc.EndObjectEdit(token);
        Assert.False(svc.IsDirty);
        Assert.True(svc.Undo()); // only the add remains
        Assert.Empty(svc.Map.Objects);
        Assert.Equal(0, svc.BeginObjectEdit(a.Id));
    }

    [Fact]
    public void UndoMidObjectEdit_ClosesEditFirst()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        var a = svc.AddObject("a", 0, 0);
        svc.BeginObjectEdit(a.Id);
        svc.UpdateObject(a with { X = 2 });

        Assert.True(svc.Undo()); // undoes the edit, not the add
        Assert.Equal(a, svc.Map.Objects[0]);
        svc.UpdateObject(a with { X = 7 }); // edit is closed now
        Assert.Equal(a, svc.Map.Objects[0]);
    }

    [Fact]
    public void SetSolid_OneUndoStep_Dirty_NoOpWhenSame()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        svc.SetSolid(1, true); // already solid
        Assert.False(svc.CanUndo);
        Assert.False(svc.IsDirty);

        svc.SetSolid(2, true);
        svc.SetSolid(1, false);
        Assert.True(svc.IsDirty);
        Assert.Equal([2, 6, 7], svc.Map.Solid.Order());

        svc.Undo();
        Assert.Equal([1, 2, 6, 7], svc.Map.Solid.Order());
        svc.Undo();
        Assert.Equal([1, 6, 7], svc.Map.Solid.Order());
        svc.Redo();
        Assert.Equal([1, 2, 6, 7], svc.Map.Solid.Order());
    }

    [Fact]
    public void Resize_KeepsSolid_NewResetsToDefault()
    {
        var svc = new MapEditingService(new TileMap(4, 4, new int[16], solid: [3]), new FakeMapRepository());
        svc.Resize(5, 5);
        Assert.Equal([3], svc.Map.Solid);
        svc.New(2, 2); // Discard must drop unsaved Solid edits too
        Assert.Equal([1, 6, 7], svc.Map.Solid.Order());
        Assert.False(svc.IsDirty);
    }

    [Fact]
    public void New_GivenSize_BlankCleanMap()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.AddObject("a", 1, 1);
        svc.New(30, 10);

        Assert.Equal((30, 10), (svc.Map.Width, svc.Map.Height));
        Assert.All(svc.Map.Tiles.ToArray(), t => Assert.Equal(0, t));
        Assert.Empty(svc.Map.Objects);
        Assert.False(svc.CanUndo);
        Assert.Null(svc.CurrentName);
        Assert.False(svc.IsDirty);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(257, 10)]
    [InlineData(10, 257)]
    [InlineData(-1, 10)]
    public void New_SizeOutOfRange_Throws(int w, int h)
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        Assert.Throws<ArgumentOutOfRangeException>(() => svc.New(w, h));
        Assert.Equal((20, 20), (svc.Map.Width, svc.Map.Height)); // map untouched
    }

    [Fact]
    public void Resize_KeepsTopLeftDropsOutsideObjects_OneUndoStep()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.BeginStroke();
        svc.Paint(new TileCoord(5, 5), 3);
        svc.Paint(new TileCoord(15, 5), 4);
        svc.EndStroke();
        var outside = svc.AddObject("out", 15, 3);
        var inside = svc.AddObject("in", 3, 3);
        Assert.True(svc.Save("a").Success);

        svc.Resize(10, 30);

        Assert.Equal((10, 30), (svc.Map.Width, svc.Map.Height));
        Assert.Equal(3, svc.Map.Get(new TileCoord(5, 5)));
        Assert.Equal(0, svc.Map.Get(new TileCoord(5, 25)));
        Assert.Equal(-1, svc.Map.IndexOf(outside.Id));
        Assert.Equal(inside, Assert.Single(svc.Map.Objects));
        Assert.True(svc.CanUndo);
        Assert.False(svc.CanRedo);
        Assert.True(svc.IsDirty);
        Assert.Equal("a", svc.CurrentName);
    }

    [Fact]
    public void Resize_Undo_RestoresMapAndOlderSteps()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.BeginStroke();
        svc.Paint(new TileCoord(5, 5), 2);
        svc.EndStroke();
        var outside = svc.AddObject("out", 15, 3);

        svc.Resize(10, 30);
        Assert.True(svc.Undo());
        Assert.Equal((20, 20), (svc.Map.Width, svc.Map.Height));
        Assert.Equal(2, svc.Map.Get(new TileCoord(5, 5)));
        Assert.Equal(outside, svc.Map.Objects[svc.Map.IndexOf(outside.Id)]);

        Assert.True(svc.Undo()); // AddObject
        Assert.True(svc.Undo()); // stroke, on the restored map
        Assert.Equal(0, svc.Map.Get(new TileCoord(5, 5)));

        Assert.True(svc.Redo());
        Assert.True(svc.Redo());
        Assert.True(svc.Redo());
        Assert.Equal((10, 30), (svc.Map.Width, svc.Map.Height));
        Assert.Equal(2, svc.Map.Get(new TileCoord(5, 5)));
    }

    [Fact]
    public void Resize_PaintAfter_UndoesStepByStep()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.Resize(10, 30);
        svc.BeginStroke();
        svc.Paint(new TileCoord(5, 25), 4);
        svc.EndStroke();

        Assert.True(svc.Undo());
        Assert.Equal((10, 30), (svc.Map.Width, svc.Map.Height));
        Assert.Equal(0, svc.Map.Get(new TileCoord(5, 25)));
        Assert.True(svc.Undo());
        Assert.Equal((20, 20), (svc.Map.Width, svc.Map.Height));
        Assert.False(svc.CanUndo);
        Assert.True(svc.Redo());
        Assert.True(svc.Redo());
        Assert.Equal(4, svc.Map.Get(new TileCoord(5, 25)));
    }

    [Fact]
    public void Resize_DuringStroke_FlushesSeparateStep()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.BeginStroke();
        svc.Paint(new TileCoord(5, 5), 2);
        svc.Resize(10, 10);

        Assert.True(svc.Undo());
        Assert.Equal((20, 20), (svc.Map.Width, svc.Map.Height));
        Assert.Equal(2, svc.Map.Get(new TileCoord(5, 5)));
        Assert.True(svc.Undo());
        Assert.Equal(0, svc.Map.Get(new TileCoord(5, 5)));
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Resize_SizeOutOfRange_ThrowsMapUntouched()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.AddObject("a", 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => svc.Resize(257, 10));
        Assert.Equal((20, 20), (svc.Map.Width, svc.Map.Height));
        Assert.True(svc.CanUndo);
    }

    [Fact]
    public void Resize_SameSize_NoOp()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.AddObject("a", 1, 1);
        var map = svc.Map;
        svc.Resize(20, 20);
        Assert.Same(map, svc.Map);
        Assert.True(svc.CanUndo);
    }

    [Fact]
    public void New_DiscardsObjectsAndOpenEdit()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        var a = svc.AddObject("a", 0, 0);
        int token = svc.BeginObjectEdit(a.Id);
        svc.New(2, 2);

        Assert.Empty(svc.Map.Objects);
        Assert.False(svc.IsDirty);
        Assert.False(svc.CanUndo);
        svc.EndObjectEdit(token); // no-op, must not touch the new map
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void EndObjectEdit_StaleToken_DoesNotCloseNewerEdit()
    {
        var svc = new MapEditingService(new TileMap(2, 2), new FakeMapRepository());
        var a = svc.AddObject("a", 0, 0);
        int inspector = svc.BeginObjectEdit(a.Id);
        svc.UpdateObject(a with { Name = "renamed" });
        int drag = svc.BeginObjectEdit(a.Id); // e.g. Scene drag starts while Inspector field is still active
        svc.EndObjectEdit(inspector);         // Inspector deactivates afterwards
        svc.UpdateObject(a with { Name = "renamed", X = 2 });
        Assert.Equal(2, svc.Map.Objects[0].X); // drag still live
        svc.EndObjectEdit(drag);

        Assert.True(svc.Undo());
        Assert.Equal(a with { Name = "renamed" }, svc.Map.Objects[0]);
        Assert.True(svc.Undo());
        Assert.Equal(a, svc.Map.Objects[0]);
    }

    private static int Count(TileMap m, int id)
    {
        int n = 0;
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < m.Width; x++)
                if (m.Get(new TileCoord(x, y)) == id) n++;
        return n;
    }

    [Fact]
    public void Fill_EmptyMap_ChangesAll_SingleUndoRevertsAll()
    {
        var svc = new MapEditingService(new TileMap(20, 20), new FakeMapRepository());
        svc.Fill(new TileCoord(5, 5), 2);
        Assert.Equal(400, Count(svc.Map, 2));
        Assert.True(svc.IsDirty);
        Assert.True(svc.Undo());
        Assert.Equal(400, Count(svc.Map, 0));
        Assert.False(svc.CanUndo);
    }

    [Fact]
    public void Fill_StopsAtWall_DiagonalGapDoesNotLeak()
    {
        // water (3) ring on 0..4 encloses the 3x3 interior; (5,5) etc. outside
        var svc = new MapEditingService(new TileMap(6, 6), new FakeMapRepository());
        svc.BeginStroke();
        for (int i = 0; i <= 4; i++)
        {
            svc.Paint(new TileCoord(i, 0), 3);
            svc.Paint(new TileCoord(i, 4), 3);
            svc.Paint(new TileCoord(0, i), 3);
            svc.Paint(new TileCoord(4, i), 3);
        }
        svc.EndStroke();
        svc.Fill(new TileCoord(2, 2), 1);
        Assert.Equal(9, Count(svc.Map, 1));
        Assert.Equal(0, svc.Map.Get(new TileCoord(5, 5)));
        Assert.Equal(0, svc.Map.Get(new TileCoord(5, 0)));
    }

    [Fact]
    public void Fill_DiagonalOnly_DoesNotSpread()
    {
        // checkerboard: every 0 cell touches other 0 cells only diagonally
        var tiles = new int[16];
        for (int i = 0; i < 16; i++) tiles[i] = (i % 4 + i / 4) % 2;
        var svc = new MapEditingService(new TileMap(4, 4, tiles), new FakeMapRepository());
        svc.Fill(new TileCoord(0, 0), 5);
        Assert.Equal(1, Count(svc.Map, 5));
    }

    [Fact]
    public void Fill_SameIdOrOutOfMap_NoOp()
    {
        var svc = new MapEditingService(new TileMap(4, 4), new FakeMapRepository());
        svc.Fill(new TileCoord(1, 1), 0);
        svc.Fill(new TileCoord(-1, 0), 2);
        svc.Fill(new TileCoord(4, 0), 2);
        Assert.False(svc.CanUndo);
        Assert.False(svc.IsDirty);
        Assert.Equal(16, Count(svc.Map, 0));
    }

    [Fact]
    public void Fill_FlushesOpenStroke_AsSeparateUndo()
    {
        var svc = new MapEditingService(new TileMap(4, 4), new FakeMapRepository());
        svc.BeginStroke();
        svc.Paint(new TileCoord(0, 0), 3);
        svc.Fill(new TileCoord(3, 3), 1);
        Assert.Equal(15, Count(svc.Map, 1));
        Assert.True(svc.Undo());
        Assert.Equal(3, svc.Map.Get(new TileCoord(0, 0)));
        Assert.Equal(15, Count(svc.Map, 0));
        Assert.True(svc.Undo());
        Assert.Equal(16, Count(svc.Map, 0));
    }

    [Fact]
    public void Fill_MaxSizeMap_FillsAll()
    {
        var svc = new MapEditingService(new TileMap(MapEditingService.MaxSize, MapEditingService.MaxSize), new FakeMapRepository());
        svc.Fill(new TileCoord(128, 128), 1);
        Assert.Equal(MapEditingService.MaxSize * MapEditingService.MaxSize, Count(svc.Map, 1));
    }
}
