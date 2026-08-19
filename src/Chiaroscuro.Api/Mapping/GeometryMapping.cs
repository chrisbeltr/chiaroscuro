using Chiaroscuro.Api.Contracts;
using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.Api.Mapping;

/// <summary>Converts between wire DTOs and Chiaroscuro.Core's geometry types.</summary>
public static class GeometryMapping
{
    public static Room ToRoom(RoomDto dto) => new(dto.Width, dto.Length, dto.Height, dto.RotationDegrees);

    public static Window ToWindow(WindowDto dto) => new(dto.Wall, dto.HorizontalOffset, dto.SillHeight, dto.Width, dto.Height);

    public static Vector3 ToVector3(Vector3Dto dto) => new(dto.X, dto.Y, dto.Z);

    public static Vector3Dto ToDto(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

    public static IlluminationDto? ToDto(IlluminationResult? result) => result is not { } hit
        ? null
        : new IlluminationDto(
            hit.Surface,
            ToDto(hit.CenterPoint),
            hit.IlluminatedPolygon.Select(ToDto).ToList(),
            hit.Patches.Select(patch => new LandingPatchDto(patch.Surface, patch.Polygon.Select(ToDto).ToList())).ToList());
}
