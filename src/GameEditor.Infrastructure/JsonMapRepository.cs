using System.Text.Json;
using GameEditor.Application;
using GameEditor.Domain;

namespace GameEditor.Infrastructure;

/// <summary>One map per file: <c>{folder}/{name}.json</c>.</summary>
public sealed class JsonMapRepository(string folder) : IMapRepository
{
    public void Save(TileMap map, string name)
    {
        var path = PathOf(name);
        Directory.CreateDirectory(folder);
        // write-then-rename so a crash mid-write never corrupts the existing map
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, JsonSerializer.SerializeToUtf8Bytes(new MapDto(map.Width, map.Height, [.. map.Tiles], [.. map.Objects]), MapJsonContext.Default.MapDto));
        File.Move(tmp, path, overwrite: true);
    }

    public TileMap Load(string name)
    {
        var dto = JsonSerializer.Deserialize(File.ReadAllBytes(PathOf(name)), MapJsonContext.Default.MapDto)
                  ?? throw new InvalidDataException("Map file is empty.");
        return new TileMap(dto.Width, dto.Height, dto.Tiles, dto.Objects);
    }

    public IReadOnlyList<string> List() =>
        Directory.Exists(folder)
            ? [.. Directory.GetFiles(folder, "*.json").Select(f => Path.GetFileNameWithoutExtension(f)).Order()]
            : [];

    private string PathOf(string name)
    {
        // GetInvalidFileNameChars() lacks '\' on Linux; ".." blocks traversal-looking names
        if (string.IsNullOrWhiteSpace(name) || name.Contains("..") || name.Contains('\\')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"Invalid map name '{name}'.");
        return Path.Combine(folder, name + ".json");
    }
}
