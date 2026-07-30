using System.Numerics;
using Chiaroscuro.Core.Geometry;
using Vector3 = Chiaroscuro.Core.Geometry.Vector3;

namespace Chiaroscuro.UI.Viewport;

/// <summary>
/// A camera that always orbits a fixed look-at point (<see cref="Target"/>) at some
/// <see cref="Distance"/>, driven purely by <see cref="Yaw"/>/<see cref="Pitch"/> angles.
/// There is deliberately no panning - the design explicitly rejected it, so the only way
/// to move the target is to assign it directly (e.g. when the room's dimensions change and
/// its center moves).
/// </summary>
public sealed class OrbitCamera
{
    private const double MinDistance = 1.0;
    private const double MaxDistance = 50.0;
    private static readonly double MaxPitchRadians = double.DegreesToRadians(89.0);

    private const float FieldOfViewRadians = (float)(Math.PI / 4.0); // 45 degrees
    private const float NearPlane = 0.1f;
    private const float FarPlane = 1000f;

    public Vector3 Target { get; set; }
    public double Yaw { get; private set; }
    public double Pitch { get; private set; }
    public double Distance { get; private set; }

    public OrbitCamera(Vector3 target, double? yaw = null, double pitch = 0.5, double distance = 8.0)
    {
        Target = target;
        Yaw = yaw ?? double.DegreesToRadians(180);
        Pitch = Math.Clamp(pitch, -MaxPitchRadians, MaxPitchRadians);
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
    }

    public void Orbit(double deltaYaw, double deltaPitch)
    {
        Yaw += -deltaYaw;
        Pitch = Math.Clamp(Pitch + deltaPitch, -MaxPitchRadians, MaxPitchRadians);
    }

    public void Zoom(double deltaDistance)
    {
        Distance = Math.Clamp(Distance + deltaDistance, MinDistance, MaxDistance);
    }

    /// <summary>The camera's own position in world space, orbiting <see cref="Target"/> at
    /// <see cref="Distance"/> - standard spherical-to-Cartesian conversion, with Yaw rotating
    /// around the world's Up axis (+Z) and Pitch tilting up/down from the horizontal.</summary>
    public Vector3 GetEyePosition()
    {
        var cosPitch = Math.Cos(Pitch);
        return new Vector3(
            Target.X + Distance * cosPitch * Math.Sin(Yaw),
            Target.Y + Distance * cosPitch * Math.Cos(Yaw),
            Target.Z + Distance * Math.Sin(Pitch));
    }

    public Matrix4x4 GetViewMatrix() =>
        Matrix4x4.CreateLookAt(ToNumerics(GetEyePosition()), ToNumerics(Target), new System.Numerics.Vector3(0, 0, 1));

    public Matrix4x4 GetProjectionMatrix(double aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewRadians, (float)aspectRatio, NearPlane, FarPlane);

    /// <summary>Converts our domain <see cref="Vector3"/> (used throughout Chiaroscuro.Core,
    /// which has no rendering dependency) into the BCL's <see cref="System.Numerics.Vector3"/>
    /// that <see cref="Matrix4x4"/> operates on.</summary>
    public static System.Numerics.Vector3 ToNumerics(Vector3 v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
