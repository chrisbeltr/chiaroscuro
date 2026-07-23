using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.Core.Solar;

/// <summary>Sun position expressed as elevation above the horizon and azimuth from true north.</summary>
public readonly record struct SolarPosition(double ElevationDegrees, double AzimuthDegrees)
{
    /// <summary>Sun unit vector per spec §3.1: S_v = (sin(θ)cos(α), cos(θ)cos(α), sin(α)).</summary>
    public Vector3 ToUnitVector()
    {
        var elevationRad = double.DegreesToRadians(ElevationDegrees);
        var azimuthRad = double.DegreesToRadians(AzimuthDegrees);

        return new Vector3(
            X: Math.Sin(azimuthRad) * Math.Cos(elevationRad),
            Y: Math.Cos(azimuthRad) * Math.Cos(elevationRad),
            Z: Math.Sin(elevationRad));
    }
}
