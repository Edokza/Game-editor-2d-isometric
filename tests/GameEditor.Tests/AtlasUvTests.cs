using GameEditor.Rendering;

namespace GameEditor.Tests;

public class AtlasUvTests
{
    // 10 cells in a 4 × 3 grid of 64 px faces: every cell's inner rect stays inside its own extruded cell
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(9)]
    public void Cell_StaysInsideItsGridCell(int slot)
    {
        const int cols = 4, rows = 3, size = 64;
        float cw = 1f / cols, ch = 1f / rows;
        var uv = Sprites.AtlasUv(slot, cols, rows, size);
        int cx = slot % cols, cy = slot / cols;
        Assert.True(uv.X > cx * cw && uv.Z < (cx + 1) * cw, $"u {uv}");
        Assert.True(uv.Y > cy * ch && uv.W < (cy + 1) * ch, $"v {uv}");
        Assert.Equal(size / (cols * (size + 2f)), uv.Z - uv.X, 5);
        Assert.Equal(size / (rows * (size + 2f)), uv.W - uv.Y, 5);
    }
}
