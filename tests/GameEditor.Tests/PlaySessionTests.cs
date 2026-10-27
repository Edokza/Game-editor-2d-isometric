using GameEditor.Application;
using GameEditor.Domain;

namespace GameEditor.Tests;

public class PlaySessionTests
{
    [Fact]
    public void EditingPlayMap_LeavesSourceUntouched()
    {
        var tree = new MapObject(1, "Tree", 2, 3, "tree");
        var source = new TileMap(4, 4, new int[16], [tree]);
        var play = new PlaySession(source);

        play.Map.Set(new TileCoord(1, 1), 5);
        play.Map.Replace(play.Map.IndexOf(play.Hero.Id), play.Hero with { X = 0.5f });
        play.Map.Replace(0, tree with { X = 9 });

        Assert.Equal(new int[16], source.Tiles.ToArray());
        Assert.Equal([tree], source.Objects);
    }

    [Fact]
    public void Hero_SpawnsAtCenterWithFreshId()
    {
        var play = new PlaySession(new TileMap(4, 6, new int[24], [new MapObject(7, "Rock", 0, 0)]));
        Assert.Equal(new MapObject(8, "Hero", 2, 3, "hero_se"), play.Hero);
        Assert.Equal(2, play.Map.Objects.Count);
    }

    [Fact]
    public void Hero_SpawnsAtFirstSpawnMarker_MarkersHidden()
    {
        MapObject[] objs = [new(1, "Tree", 1, 1, "tree"), new(2, "Spawn", 2.5f, 3.5f, "spawn"), new(3, "Spawn", 0.5f, 0.5f, "spawn")];
        var source = new TileMap(4, 6, new int[24], objs);
        var play = new PlaySession(source);

        Assert.Equal((2.5f, 3.5f), (play.Hero.X, play.Hero.Y));
        Assert.DoesNotContain(play.Map.Objects, o => o.Sprite == "spawn");
        Assert.Equal(["Tree", "Hero"], play.Map.Objects.Select(o => o.Name));
        Assert.Equal(objs, source.Objects);
    }

    [Fact]
    public void Hero_SkipsSpawnOutsideMap()
    {
        var play = new PlaySession(new TileMap(4, 6, new int[24], [new(1, "Spawn", -0.5f, 1, "spawn"), new(2, "Spawn", 1, 6, "spawn")]));
        Assert.Equal((2f, 3f), (play.Hero.X, play.Hero.Y));
        Assert.Single(play.Map.Objects);
    }

    // 4×4 map, hero at (2,2); water (id 1) at the listed tiles
    private static PlaySession Session(params (int X, int Y)[] water)
    {
        var map = new TileMap(4, 4);
        foreach (var (x, y) in water) map.Set(new TileCoord(x, y), 1);
        return new PlaySession(map);
    }

    private static void Walk(PlaySession play, float dx, float dy, int frames = 60)
    {
        for (int i = 0; i < frames; i++) play.Move(new(dx, dy), 1 / 60f);
    }

    [Fact]
    public void Move_StopsBeforeSolidTile()
    {
        var play = Session((3, 2));
        Walk(play, 1, 0);
        Assert.True(play.Hero.X is > 2.9f and < 3);
        Assert.Equal(2, play.Hero.Y);
    }

    [Fact]
    public void Move_DiagonalIntoWall_SlidesAlongIt()
    {
        var play = Session((3, 0), (3, 1), (3, 2), (3, 3));
        Walk(play, 1, 1, 40);
        Assert.True(play.Hero.X < 3);
        Assert.True(play.Hero.Y > 3.5f);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 1)]
    public void Move_CannotLeaveMap(float dx, float dy)
    {
        var play = Session();
        Walk(play, dx, dy, 120);
        Assert.True(play.Hero.X is >= 0 and < 4);
        Assert.True(play.Hero.Y is >= 0 and < 4);
    }

    [Fact]
    public void Move_UsesMapSolid_CopiedFromSource()
    {
        var source = new TileMap(4, 4, new int[16], solid: [0]); // every tile is id 0
        var play = new PlaySession(source);
        Walk(play, 1, 0);
        Assert.True(play.Hero.X < 3); // can't leave tile (2,2)

        play.Map.SetSolid(0, false);
        Assert.Contains(0, source.Solid); // copy, not shared
        Walk(play, 1, 0);
        Assert.True(play.Hero.X >= 3);
    }

    [Fact]
    public void SolidSprites_CopiedFromSource()
    {
        var source = new TileMap(4, 4, new int[16], solidSprites: ["bush"]);
        var play = new PlaySession(source);
        Assert.Equal(["bush"], play.Map.SolidSprites);

        play.Map.SetSolidSprite("bush", false);
        Assert.Equal(["bush"], source.SolidSprites); // copy, not shared
    }

    // 8×8 map, hero spawned at (spawnX, spawnY), one object with the given sprite at (5.5, 5.5)
    private static PlaySession WithObject(string sprite, float spawnX, float spawnY) =>
        new(new TileMap(8, 8, new int[64], [new(1, "Obj", 5.5f, 5.5f, sprite), new(2, "Spawn", spawnX, spawnY, PlaySession.SpawnSprite)]));

    [Fact]
    public void Move_StopsBeforeSolidObject()
    {
        var play = WithObject("tree", 4.5f, 5.5f);
        Walk(play, 1, 0);
        Assert.True(play.Hero.X is > 4.9f and < 5);
    }

    [Fact]
    public void Move_NonSolidObject_WalksThrough()
    {
        var play = WithObject("bush", 4.5f, 5.5f);
        Walk(play, 1, 0);
        Assert.True(play.Hero.X >= 6);
    }

    [Fact]
    public void Move_SpawnedOnSolidObject_CanWalkOut()
    {
        var play = WithObject("tree", 5.5f, 5.5f);
        Walk(play, -1, 0);
        Assert.True(play.Hero.X < 5);
    }

    [Fact]
    public void Move_SpawnedInSolid_CanWalkOut()
    {
        var play = Session((2, 2));
        Walk(play, 1, 0);
        Assert.True(play.Hero.X >= 3);
    }

    [Fact]
    public void Move_ZeroOrHugeDt_Safe()
    {
        var play = Session();
        var before = play.Hero;
        play.Move(default, 1 / 60f);
        Assert.Equal(before, play.Hero);
        play.Move(new(1, 0), 10); // clamped: at most Speed × 0.1 tiles
        Assert.Equal(2 + PlaySession.Speed * 0.1f, play.Hero.X, 0.0001f);
    }

    [Fact]
    public void Move_SpriteFacesScreenDirection()
    {
        var play = Session();
        play.Move(new(-1, -1), 1 / 60f);
        Assert.Equal("hero_ne", play.Hero.Sprite);
        play.Move(new(1, 0), 1 / 60f);
        Assert.Equal("hero_se", play.Hero.Sprite);
    }

    [Fact]
    public void Move_SpriteFacesFourWays()
    {
        var play = Session();
        play.Move(new(1, -1), 1 / 60f); // D
        Assert.Equal("hero_ne", play.Hero.Sprite);
        play.Move(new(-1, 1), 1 / 60f); // A
        Assert.Equal("hero_sw", play.Hero.Sprite);
        play.Move(new(-1, -1), 1 / 60f); // W keeps the left side
        Assert.Equal("hero_nw", play.Hero.Sprite);
        play.Move(default, 1 / 60f);
        Assert.Equal("hero_nw", play.Hero.Sprite);
    }

    public static TheoryData<int> Rotations => [0, 1, 2, 3];

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Move_W_GoesUpTheScreen_AtEveryRotation(int r)
    {
        var play = new PlaySession(new TileMap(8, 8));
        var view = new IsoView(r, 8, 8);
        var before = view.TileToScreen(play.Hero.X, play.Hero.Y);
        for (int i = 0; i < 10; i++) play.Move(new(-1, -1), 1 / 60f, view); // W
        var after = view.TileToScreen(play.Hero.X, play.Hero.Y);
        Assert.True(after.Y < before.Y, $"{before} -> {after}");
        Assert.Equal(before.X, after.X, 3);
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Move_SpriteFacesScreenDirection_AtEveryRotation(int r)
    {
        var play = new PlaySession(new TileMap(8, 8));
        var view = new IsoView(r, 8, 8);
        play.Move(new(1, -1), 1 / 60f, view); // D
        Assert.Equal("hero_ne", play.Hero.Sprite);
        play.Move(new(-1, 1), 1 / 60f, view); // A
        Assert.Equal("hero_sw", play.Hero.Sprite);
        play.Move(new(1, 1), 1 / 60f, view); // S keeps the left side
        Assert.Equal("hero_sw", play.Hero.Sprite);
    }
}
