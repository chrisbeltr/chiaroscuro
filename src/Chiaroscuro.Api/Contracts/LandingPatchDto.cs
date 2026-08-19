using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.Api.Contracts;

public sealed record LandingPatchDto(RoomSurface Surface, IReadOnlyList<Vector3Dto> Polygon);
