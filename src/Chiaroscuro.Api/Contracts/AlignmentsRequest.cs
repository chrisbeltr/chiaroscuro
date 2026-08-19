namespace Chiaroscuro.Api.Contracts;

/// <summary>
/// Mirrors the inverse-solver branch of MainViewModel.Recalculate(): a room/window/location
/// plus a target point and tolerance, swept across a year starting at Year/Month/Day.
/// </summary>
public sealed record AlignmentsRequest(
    RoomDto Room,
    WindowDto Window,
    Vector3Dto Target,
    double Latitude,
    double Longitude,
    double UtcOffsetHours,
    int Year,
    int Month,
    int Day,
    double ToleranceDegrees,
    int MaxResults = 15);
