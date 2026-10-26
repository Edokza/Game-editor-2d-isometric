using GameEditor.Infrastructure;

namespace GameEditor.Tests;

public sealed class GameExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}");
    private string Exe => Path.Combine(_root, "player.exe");
    private string Map => Path.Combine(_root, "map.json");
    private string Out => Path.Combine(_root, "export");

    public GameExporterTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Exe, [1, 2, 3]);
        File.WriteAllText(Map, "{\"Width\":1}");
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Export_CopiesExeAndMapUnderName()
    {
        var r = GameExporter.Export(Exe, Map, Out, "level1");

        Assert.True(r.Success, r.Message);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(Out, "level1", "level1.exe")));
        Assert.Equal("{\"Width\":1}", File.ReadAllText(Path.Combine(Out, "level1", "maps", "level1.json")));
    }

    [Fact]
    public void ExportAgain_Overwrites()
    {
        GameExporter.Export(Exe, Map, Out, "a");
        File.WriteAllText(Map, "{\"Width\":2}");

        Assert.True(GameExporter.Export(Exe, Map, Out, "a").Success);
        Assert.Equal("{\"Width\":2}", File.ReadAllText(Path.Combine(Out, "a", "maps", "a.json")));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../x")]
    [InlineData("a\\b")]
    [InlineData(" ")]
    public void BadName_FailsAndWritesNothing(string name)
    {
        var r = GameExporter.Export(Exe, Map, Out, name);

        Assert.False(r.Success);
        Assert.False(Directory.Exists(Out));
    }

    [Fact]
    public void MissingPlayer_FailsWithPath()
    {
        var missing = Path.Combine(_root, "nope.exe");

        var r = GameExporter.Export(missing, Map, Out, "a");

        Assert.False(r.Success);
        Assert.Contains("Player not found", r.Message);
        Assert.Contains(missing, r.Message);
    }

    [Fact]
    public void LockedTarget_FailsInsteadOfThrowing()
    {
        GameExporter.Export(Exe, Map, Out, "a");
        using var running = File.Open(Path.Combine(Out, "a", "a.exe"), FileMode.Open, FileAccess.Read, FileShare.None); // like the game still open

        Assert.False(GameExporter.Export(Exe, Map, Out, "a").Success);
    }
}
