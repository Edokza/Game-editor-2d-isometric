using System.Numerics;
using GameEditor.Domain;

namespace GameEditor.Tests;

public class IsoViewTests
{
    // non-square: a swapped width/height bug can't hide
    private const int W = 5, H = 3;

    public static TheoryData<int> Rotations => [0, 1, 2, 3];

    private static IEnumerable<TileCoord> Cells()
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                yield return new(x, y);
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Cells_MapOneToOneIntoViewBounds(int r)
    {
        var view = new IsoView(r, W, H);
        var seen = new HashSet<TileCoord>();
        foreach (var c in Cells())
        {
            var v = view.ToView(c);
            Assert.InRange(v.X, 0, view.ViewWidth - 1);
            Assert.InRange(v.Y, 0, view.ViewHeight - 1);
            Assert.True(seen.Add(v), $"{c} collides at {v}");
            Assert.Equal(c, view.FromView(v));
        }
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void Points_RoundTrip(int r)
    {
        var view = new IsoView(r, W, H);
        foreach (var p in (Vector2[])[new(0, 0), new(1.5f, 2.25f), new(4.75f, 0.5f)])
        {
            var back = view.FromView(view.ToView(p.X, p.Y));
            Assert.Equal(p.X, back.X, 4);
            Assert.Equal(p.Y, back.Y, 4);
        }
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void PointInsideCell_StaysInsideRotatedCell(int r)
    {
        var view = new IsoView(r, W, H);
        foreach (var c in Cells())
        {
            var v = view.ToView(c.X + 0.25f, c.Y + 0.75f);
            Assert.Equal(view.ToView(c), new TileCoord((int)MathF.Floor(v.X), (int)MathF.Floor(v.Y)));
        }
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void ScreenToTile_InvertsTileToScreen(int r)
    {
        var view = new IsoView(r, W, H);
        foreach (var c in Cells())
        {
            var top = view.TileToScreen(c);
            Assert.Equal(c, view.ScreenToTile(top));
            Assert.Equal(c, view.ScreenToTile(top + new Vector2(0, IsoMath.TileHeight / 2f)));
        }
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void ScreenToTileF_InvertsFloatTileToScreen(int r)
    {
        var view = new IsoView(r, W, H);
        var t = view.ScreenToTileF(view.TileToScreen(1.5f, 2.25f));
        Assert.Equal(1.5f, t.X, 4);
        Assert.Equal(2.25f, t.Y, 4);
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void NonFinite_NeverInBounds(int r)
    {
        var view = new IsoView(r, W, H);
        var map = new TileMap(W, H);
        Assert.False(map.InBounds(view.ScreenToTile(new Vector2(float.NaN, 0))));
        Assert.False(map.InBounds(view.ScreenToTile(new Vector2(0, float.PositiveInfinity))));
    }

    [Theory]
    [MemberData(nameof(Rotations))]
    public void DirToView_MatchesPointDifference(int r)
    {
        var view = new IsoView(r, W, H);
        var d = new Vector2(0.5f, -0.25f);
        var moved = view.ToView(1 + d.X, 2 + d.Y) - view.ToView(1, 2);
        Assert.Equal(moved, view.DirToView(d));
        Assert.Equal(d, view.DirFromView(view.DirToView(d)));
    }

    [Fact]
    public void Rotation_Wraps()
    {
        Assert.Equal(1, new IsoView(5, W, H).Rotation);
        Assert.Equal(3, new IsoView(-1, W, H).Rotation);
        Assert.Equal(2, new IsoView(1, W, H).Rotated(1).Rotation);
        Assert.Equal(0, new IsoView(3, W, H).Rotated(1).Rotation);
    }

    [Fact]
    public void FourSteps_IsIdentity()
    {
        var view = new IsoView(1, W, H).Rotated(4);
        foreach (var c in Cells()) Assert.Equal(new IsoView(1, W, H).ToView(c), view.ToView(c));
    }

    [Theory]
    [InlineData(0, W, H)]
    [InlineData(1, H, W)]
    [InlineData(2, W, H)]
    [InlineData(3, H, W)]
    public void OddRotations_SwapSides(int r, int viewW, int viewH)
    {
        var view = new IsoView(r, W, H);
        Assert.Equal(viewW, view.ViewWidth);
        Assert.Equal(viewH, view.ViewHeight);
    }

    [Fact]
    public void Default_MatchesIsoMath()
    {
        var view = default(IsoView);
        var c = new TileCoord(3, 7);
        Assert.Equal(IsoMath.TileToScreen(c), view.TileToScreen(c));
        Assert.Equal(IsoMath.TileToScreen(1.5f, 2.5f), view.TileToScreen(1.5f, 2.5f));
        Assert.Equal(c, view.ScreenToTile(IsoMath.TileToScreen(c)));
    }

    [Theory]
    [InlineData(0, W - 1, H - 1)] // unrotated: far corner nearest, as today
    [InlineData(1, W - 1, 0)]  // view (H-1-y, x): big x, small y comes forward
    [InlineData(2, 0, 0)]       // turned around: tile (0,0) is now in front
    [InlineData(3, 0, H - 1)]
    public void Depth_NearestCellPerRotation(int r, int x, int y)
    {
        var view = new IsoView(r, W, H);
        var nearest = Cells().MaxBy(c => view.Depth(c.X + 0.5f, c.Y + 0.5f));
        Assert.Equal(new TileCoord(x, y), nearest);
    }
}
