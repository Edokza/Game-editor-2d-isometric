using GameEditor.Application;
using GameEditor.Domain;

namespace GameEditor.Tests;

public class PaintTilesCommandTests
{
    [Fact]
    public void Execute_ThenUndo_RestoresEveryCell()
    {
        var map = new TileMap(3, 3, [1, 1, 1, 2, 2, 2, 3, 3, 3]);
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 1);
        var c = new TileCoord(2, 2);
        var cmd = new PaintTilesCommand(map, new Dictionary<TileCoord, (int, int)>
        {
            [a] = (1, 9), [b] = (2, 9), [c] = (3, 9),
        });

        cmd.Execute();
        Assert.All([a, b, c], t => Assert.Equal(9, map.Get(t)));
        Assert.Equal(1, map.Get(new TileCoord(1, 0))); // untouched

        cmd.Undo();
        Assert.Equal(1, map.Get(a));
        Assert.Equal(2, map.Get(b));
        Assert.Equal(3, map.Get(c));
    }
}
