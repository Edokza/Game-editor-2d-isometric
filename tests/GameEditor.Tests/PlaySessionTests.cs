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
}
