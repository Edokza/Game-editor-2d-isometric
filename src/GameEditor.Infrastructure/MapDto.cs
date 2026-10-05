using System.Text.Json.Serialization;
using GameEditor.Domain;

namespace GameEditor.Infrastructure;

/// <summary><see cref="Objects"/> optional: files saved before objects existed have none.</summary>
internal sealed record MapDto(int Width, int Height, int[] Tiles, MapObject[]? Objects = null);

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(MapDto))]
internal sealed partial class MapJsonContext : JsonSerializerContext;
