using GameEditor.Application;
using GameEditor.Domain;
using GameEditor.Infrastructure;

namespace GameEditor.Tests;

public sealed class JsonMapRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"maps-{Guid.NewGuid():N}");
    private string Folder => Path.Combine(_root, "maps");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void TwoNames_ListedAndEachLoadsItsOwnMap()
    {
        var repo = new JsonMapRepository(Folder);
        var a = new TileMap(3, 2, [0, 1, 2, 3, 4, 5]);
        var b = new TileMap(2, 2, [5, 5, 5, 5]);

        repo.Save(a, "a");
        repo.Save(b, "b");

        Assert.Equal(["a", "b"], repo.List());
        var la = repo.Load("a");
        Assert.Equal((3, 2), (la.Width, la.Height));
        Assert.Equal(a.Tiles.ToArray(), la.Tiles.ToArray());
        Assert.Equal(b.Tiles.ToArray(), repo.Load("b").Tiles.ToArray());
    }

    [Fact]
    public void SaveOverExisting_ReplacesAndLeavesNoTempFile()
    {
        var repo = new JsonMapRepository(Folder);
        repo.Save(new TileMap(1, 1, [1]), "a");
        repo.Save(new TileMap(1, 1, [2]), "a");

        Assert.Equal(2, repo.Load("a").Get(new TileCoord(0, 0)));
        Assert.Equal(["a.json"], Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void NoFolder_ListIsEmpty() => Assert.Empty(new JsonMapRepository(Folder).List());

    [Theory]
    [InlineData("../x")]
    [InlineData(@"..\x")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData("   ")]
    public void BadName_FailsAndWritesNothing(string name)
    {
        var svc = new MapEditingService(new TileMap(1, 1), new JsonMapRepository(Folder));

        Assert.False(svc.Save(name).Success);
        Assert.False(svc.Load(name).Success);
        Assert.False(Directory.Exists(_root)); // nothing created anywhere under root, incl. ../x.json
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"Width":3,"Height":2,"Tiles":[1,2]}""")] // size mismatch
    [InlineData("""{"Width":3,"Height":2}""")]               // missing tiles
    [InlineData("""{"Width":3,"Height":2,"Tiles":null}""")]
    [InlineData("")]
    [InlineData("""{"Width":1,"Height":1,"Tiles":[0],"Objects":[{"Id":1,"Name":"a","X":0,"Y":0},{"Id":1,"Name":"b","X":0,"Y":0}]}""")] // duplicate id
    [InlineData("""{"Width":1,"Height":1,"Tiles":[0],"Objects":[{"Id":1,"Name":null,"X":0,"Y":0}]}""")]
    [InlineData("""{"Width":1,"Height":1,"Tiles":[0],"Objects":[{"Id":1,"X":0,"Y":0}]}""")] // missing name
    [InlineData("""{"Width":1,"Height":1,"Tiles":[0],"Objects":[null]}""")]
    public void CorruptFile_LoadReturnsFailure(string json)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "bad.json"), json);
        var map = new TileMap(1, 1);
        var svc = new MapEditingService(map, new JsonMapRepository(Folder));

        var result = svc.Load("bad");

        Assert.False(result.Success);
        Assert.Same(map, svc.Map);
    }

    [Fact]
    public void MissingFile_LoadReturnsFailure() =>
        Assert.False(new MapEditingService(new TileMap(1, 1), new JsonMapRepository(Folder)).Load("nope").Success);

    [Fact]
    public void Objects_RoundTrip()
    {
        var repo = new JsonMapRepository(Folder);
        MapObject[] objects = [new(3, "tree", 1.5f, 2.25f), new(1, "rock %", -1, 0)];
        repo.Save(new TileMap(1, 1, [0], objects), "a");

        Assert.Equal(objects, repo.Load("a").Objects);
    }

    [Fact]
    public void OldFileWithoutObjects_LoadsEmpty()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "old.json"), """{"Width":1,"Height":1,"Tiles":[2]}""");

        var map = new JsonMapRepository(Folder).Load("old");

        Assert.Equal(2, map.Get(new TileCoord(0, 0)));
        Assert.Empty(map.Objects);
    }
}
