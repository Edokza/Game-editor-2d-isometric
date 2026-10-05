using GameEditor.Domain;

namespace GameEditor.Tests;

public class TileMapTests
{
    [Fact]
    public void Ctor_WrongSize_Throws() =>
        Assert.Throws<ArgumentException>(() => new TileMap(3, 2, new int[5]));

    [Fact]
    public void Ctor_UsesRowMajorIndex()
    {
        int[] tiles = [0, 1, 2, 3, 4, 5];
        var map = new TileMap(3, 2, tiles);
        Assert.Equal(5, map.Get(new TileCoord(2, 1))); // 1*3+2
        Assert.Equal(1, map.Get(new TileCoord(1, 0)));
    }

    [Fact]
    public void SetThenGet_SameCell()
    {
        var map = new TileMap(4, 3);
        map.Set(new TileCoord(1, 2), 7);
        Assert.Equal(7, map.Get(new TileCoord(1, 2)));
        Assert.Equal(0, map.Get(new TileCoord(2, 1)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    public void OutOfBounds_DoesNotThrow(int x, int y)
    {
        var map = new TileMap(4, 3);
        var c = new TileCoord(x, y);
        Assert.False(map.InBounds(c));
        map.Set(c, 9);
        Assert.Equal(0, map.Get(c));
    }
}
