using Chiaroscuro.Core.Geometry;
using Chiaroscuro.UI.Viewport;
using Xunit;

namespace Chiaroscuro.UI.Tests.Viewport;

public class OrbitCameraTests
{
    [Fact]
    public void Orbit_ClampsPitchToEightyNineDegrees()
    {
        var camera = new OrbitCamera(Vector3.Zero);

        camera.Orbit(deltaYaw: 0, deltaPitch: 100.0); // way past vertical

        Assert.Equal(double.DegreesToRadians(89.0), camera.Pitch);
    }

    [Fact]
    public void Orbit_ClampsPitchToNegativeEightyNineDegrees()
    {
        var camera = new OrbitCamera(Vector3.Zero);

        camera.Orbit(deltaYaw: 0, deltaPitch: -100.0);

        Assert.Equal(-double.DegreesToRadians(89.0), camera.Pitch);
    }

    [Fact]
    public void Zoom_ClampsDistanceToUpperBound()
    {
        var camera = new OrbitCamera(Vector3.Zero, distance: 5.0);

        camera.Zoom(1000.0);

        Assert.Equal(50.0, camera.Distance);
    }

    [Fact]
    public void Zoom_ClampsDistanceToLowerBound()
    {
        var camera = new OrbitCamera(Vector3.Zero, distance: 5.0);

        camera.Zoom(-1000.0);

        Assert.Equal(1.0, camera.Distance);
    }

    [Fact]
    public void Orbit_By180Degrees_SwapsWhichSideIsCloserToCamera()
    {
        var target = Vector3.Zero;
        var camera = new OrbitCamera(target, yaw: 0.0, pitch: 0.0, distance: 10.0);
        var northPoint = new Vector3(0, 5, 0);
        var southPoint = new Vector3(0, -5, 0);

        var eyeBefore = camera.GetEyePosition();
        Assert.True((eyeBefore - northPoint).Length < (eyeBefore - southPoint).Length);

        camera.Orbit(deltaYaw: Math.PI, deltaPitch: 0.0);

        var eyeAfter = camera.GetEyePosition();
        Assert.True((eyeAfter - southPoint).Length < (eyeAfter - northPoint).Length);
    }
}
