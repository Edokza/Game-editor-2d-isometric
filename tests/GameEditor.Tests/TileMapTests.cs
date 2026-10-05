using GameEditor.Domain;

namespace GameEditor.Tests;

public class TileMapTests
{
    [Fact]
    public void Ctor_WrongSize_Throws() =>
        Assert.Throws<ArgumentException>(() => new TileMap(3, 2, new int[5]));

    [Fact]
    public void Ctor_SizeOverflowingInt_Throws() =>
        Assert.Throws<ArgumentException>(() => new TileMap(65536, 65536, [])); // 65536*65536 wraps to 0 in int

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

    [Fact]
    public void Ctor_DuplicateObjectId_Throws() =>
        Assert.Throws<ArgumentException>(() => new TileMap(1, 1, [0], [new(1, "a", 0, 0), new(1, "b", 1, 1)]));

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.PositiveInfinity)]
    public void Ctor_NonFiniteObjectPosition_Throws(float x, float y) =>
        Assert.Throws<ArgumentException>(() => new TileMap(1, 1, [0], [new(1, "a", x, y)]));

    [Fact]
    public void InsertRemoveReplace_KeepOrderAndIds()
    {
        var map = new TileMap(1, 1);
        Assert.Equal(1, map.NextObjectId);

        map.Insert(0, new(5, "a", 0, 0));
        map.Insert(0, new(2, "b", 0, 0));
        Assert.Equal([2, 5], map.Objects.Select(o => o.Id));
        Assert.Equal(6, map.NextObjectId);
        Assert.Equal(1, map.IndexOf(5));
        Assert.Equal(-1, map.IndexOf(9));

        map.Replace(1, new(5, "a2", 3, 4)); // same id at own index is fine
        Assert.Equal(new MapObject(5, "a2", 3, 4), map.Objects[1]);
        Assert.Throws<ArgumentException>(() => map.Replace(1, new(2, "dup", 0, 0)));
        Assert.Throws<ArgumentException>(() => map.Insert(0, new(5, "dup", 0, 0)));

        map.RemoveAt(0);
        Assert.Equal([5], map.Objects.Select(o => o.Id));
    }

    [Fact]
    public void NextObjectId_MaxIdTaken_UsesLowestFreeInsteadOfWrapping()
    {
        var map = new TileMap(1, 1, [0], [new(int.MaxValue, "m", 0, 0), new(1, "a", 0, 0)]);
        Assert.Equal(2, map.NextObjectId);
        map.Insert(map.Objects.Count, new(map.NextObjectId, "b", 0, 0));
        Assert.Equal(3, map.NextObjectId);
    }
}
