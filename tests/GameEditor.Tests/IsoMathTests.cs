using System.Numerics;
using GameEditor.Domain;

namespace GameEditor.Tests;

public class IsoMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 7)]
    [InlineData(-5, 2)]
    [InlineData(99, 99)]
    public void RoundTrip_TopVertexAndCenter(int x, int y)
    {
        var c = new TileCoord(x, y);
        var top = IsoMath.TileToScreen(c);
        Assert.Equal(c, IsoMath.ScreenToTile(top));
        Assert.Equal(c, IsoMath.ScreenToTile(top + new Vector2(0, IsoMath.TileHeight / 2f)));
    }

    [Theory]
    // tile (0,0) diamond: top (0,0), right (32,16), bottom (0,32), left (-32,16)
    [InlineData(31, 16, 0, 0)]   // just inside right vertex
    [InlineData(33, 16, 1, -1)]  // just past right vertex
    [InlineData(-31, 16, 0, 0)]  // just inside left vertex
    [InlineData(-33, 16, -1, 1)] // just past left vertex
    [InlineData(0, 31, 0, 0)]    // just above bottom vertex
    [InlineData(0, 33, 1, 1)]    // just below bottom vertex
    [InlineData(15, 8, 0, 0)]    // below top-right edge y=x/2 -> inside
    [InlineData(17, 8, 0, -1)]   // above top-right edge -> neighbor (0,-1)
    public void EdgePoints_FallInCorrectTile(float sx, float sy, int x, int y) =>
        Assert.Equal(new TileCoord(x, y), IsoMath.ScreenToTile(new Vector2(sx, sy)));

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.NaN)]
    [InlineData(float.PositiveInfinity, 0)]
    [InlineData(0, float.NegativeInfinity)]
    public void NonFinite_NeverInBounds(float sx, float sy) =>
        Assert.False(new TileMap(20, 20).InBounds(IsoMath.ScreenToTile(new Vector2(sx, sy))));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1.5f, 2.25f)]
    [InlineData(-3.75f, 10.5f)]
    public void ScreenToTileF_InvertsFloatTileToScreen(float x, float y)
    {
        var t = IsoMath.ScreenToTileF(IsoMath.TileToScreen(x, y));
        Assert.Equal(x, t.X, 4);
        Assert.Equal(y, t.Y, 4);
    }
}
