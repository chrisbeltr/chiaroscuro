namespace Chiaroscuro.Api.Contracts;

public sealed record AlignmentMatchDto(
    int Year,
    int Month,
    int Day,
    int Hour,
    int Minute,
    double ElevationDegrees,
    double AzimuthDegrees,
    double AngleDifferenceDegrees);
