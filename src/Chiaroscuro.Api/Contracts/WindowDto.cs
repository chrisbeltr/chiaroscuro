using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.Api.Contracts;

public sealed record WindowDto(WallOrientation Wall, double HorizontalOffset, double SillHeight, double Width, double Height);
