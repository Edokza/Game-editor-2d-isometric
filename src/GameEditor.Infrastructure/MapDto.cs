using System.Text.Json.Serialization;
using GameEditor.Domain;

namespace GameEditor.Infrastructure;

/// <summary><see cref="Objects"/>/<see cref="Solid"/> optional: older files have none (Solid null → <see cref="TileMap.DefaultSolid"/>).</summary>
internal sealed record MapDto(int Width, int Height, int[] Tiles, MapObject[]? Objects = null, int[]? Solid = null);

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(MapDto))]
internal sealed partial class MapJsonContext : JsonSerializerContext;
