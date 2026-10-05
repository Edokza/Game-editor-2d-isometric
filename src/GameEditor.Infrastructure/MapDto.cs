using System.Text.Json.Serialization;

namespace GameEditor.Infrastructure;

internal sealed record MapDto(int Width, int Height, int[] Tiles);

[JsonSourceGenerationOptions(RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(MapDto))]
internal sealed partial class MapJsonContext : JsonSerializerContext;
