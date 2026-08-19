using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.Api.Contracts;

public sealed record IlluminationDto(
    RoomSurface Surface,
    Vector3Dto CenterPoint,
    IReadOnlyList<Vector3Dto> IlluminatedPolygon,
    IReadOnlyList<LandingPatchDto> Patches);
