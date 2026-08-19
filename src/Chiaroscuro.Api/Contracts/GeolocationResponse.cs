namespace Chiaroscuro.Api.Contracts;

/// <summary>
/// Success is false when the underlying IP lookup failed (network error, service down,
/// etc.) - mirroring IpGeolocation.cs's swallow-and-return-null contract - in which case
/// the coordinate fields are all null and the caller falls back to manual entry.
/// </summary>
public sealed record GeolocationResponse(bool Success, double? Latitude, double? Longitude, double? UtcOffsetHours);
