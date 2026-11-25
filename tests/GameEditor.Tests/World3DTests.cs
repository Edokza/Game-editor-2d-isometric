using System.Numerics;
using GameEditor.Domain;

namespace GameEditor.Tests;

public class World3DTests
{
    private static readonly Vector3 Down = -Vector3.UnitY;

    [Fact]
    public void Pick_StraightDown_HitsCellBelow()
    {
        var map = new TileMap(5, 3);
        Assert.Equal(new TileCoord(2, 1), World3D.Pick(new(2.5f, 10, 1.5f), Down, map)?.Cell);
    }

    [Fact]
    public void Pick_Oblique_TallColumnInFrontWins()
    {
        var map = new TileMap(6, 1);
        // flat: from (0.5, 3) going +x and down 1:1 lands at x = 3.5 → cell 3
        var d = Vector3.Normalize(new(1, -1, 0));
        Assert.Equal(new TileCoord(3, 0), World3D.Pick(new(0.5f, 3, 0.5f), d, map)?.Cell);
        map.SetHeight(new(2, 0), 8); // top 4 > ray height over cell 2
        Assert.Equal(new TileCoord(2, 0), World3D.Pick(new(0.5f, 3, 0.5f), d, map)?.Cell);
    }

    [Fact]
    public void Pick_FromOutsideMap_EntersAndHits()
    {
        var map = new TileMap(4, 4);
        var d = Vector3.Normalize(new(1, -1, 1));
        Assert.Equal(new TileCoord(1, 1), World3D.Pick(new(-1.5f, 3, -1.5f), d, map)?.Cell);
    }

    [Fact]
    public void Pick_Misses_ReturnsNull()
    {
        var map = new TileMap(4, 4);
        Assert.Null(World3D.Pick(new(10, 5, 10), Down, map)?.Cell);
        Assert.Null(World3D.Pick(new(1, 5, 1), Vector3.UnitY, map)?.Cell);
        Assert.Null(World3D.Pick(new(-1, 5, 1), -Vector3.UnitX, map)?.Cell);
    }

    [Fact]
    public void Pick_CustomTop()
    {
        var map = new TileMap(6, 1);
        var d = Vector3.Normalize(new(1, -1, 0));
        Assert.Equal(new TileCoord(1, 0), World3D.Pick(new(0.5f, 3, 0.5f), d, map, c => c.X == 1 ? 3 : 0)?.Cell);
    }

    [Fact]
    public void Pick_T_IsDistanceToTop()
    {
        var map = new TileMap(3, 3);
        map.SetHeight(new(1, 1), 4); // top 2
        Assert.Equal(8, World3D.Pick(new(1.5f, 10, 1.5f), Down, map)!.Value.T, 4);
    }

    [Fact]
    public void PlaneHit()
    {
        Assert.Equal(new Vector2(2, 3), World3D.PlaneHit(new(2, 5, 3), Down, 1));
        Assert.Null(World3D.PlaneHit(new(2, 5, 3), Vector3.UnitX, 1));
        Assert.Null(World3D.PlaneHit(new(2, 5, 3), Vector3.UnitY, 1));
    }

    // yaw 90 × n = old view rotation n: data direction of screen-up (W) per rotation
    [Theory]
    [InlineData(0, -1, -1)]
    [InlineData(90, -1, 1)]
    [InlineData(180, 1, 1)]
    [InlineData(270, 1, -1)]
    public void DirFromScreen_W(int yaw, int x, int y) =>
        Assert.Equal(new Vector2(x, y), World3D.DirFromScreen(new(-1, -1), yaw));
}
