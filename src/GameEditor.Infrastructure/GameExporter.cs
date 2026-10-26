using GameEditor.Application;

namespace GameEditor.Infrastructure;

/// <summary>Playable folder: <c>{outDir}/{name}/{name}.exe</c> + <c>maps/{name}.json</c> (Player reads <c>maps</c> next to its exe).</summary>
public static class GameExporter
{
    public static Result Export(string playerExe, string mapJson, string outDir, string name)
    {
        if (!JsonMapRepository.IsValidName(name)) return new(false, $"Invalid game name '{name}'.");
        if (!File.Exists(playerExe)) return new(false, $"Player not found: {playerExe}");
        if (!File.Exists(mapJson)) return new(false, $"Map not found: {mapJson}");
        try
        {
            var dir = Path.Combine(outDir, name);
            var maps = Directory.CreateDirectory(Path.Combine(dir, "maps")).FullName;
            // map first: the usual failure is the exe still running (locked), and the old exe plays the new map fine
            File.Copy(mapJson, Path.Combine(maps, name + ".json"), overwrite: true);
            File.Copy(playerExe, Path.Combine(dir, name + ".exe"), overwrite: true);
            return new(true, $"Exported to {dir}");
        }
        catch (Exception e) // locked exe (game still running), read-only folder, bad outDir...
        {
            return new(false, $"Export failed: {e.Message}");
        }
    }
}
