namespace Chiaroscuro.Api.Contracts;

/// <summary>
/// Mirrors the input side of MainViewModel.Recalculate(): a room/window/location/moment
/// combination, from which the sun position and (if any) illuminated surface are derived.
/// </summary>
public sealed record IlluminationRequest(
    RoomDto Room,
    WindowDto Window,
    double Latitude,
    double Longitude,
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    double UtcOffsetHours);
