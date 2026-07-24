using Chiaroscuro.Core.Geometry;
using Chiaroscuro.UI.Viewport;
using Xunit;

namespace Chiaroscuro.UI.Tests.Viewport;

public class ViewportProjectorTests
{
    [Theory]
    [InlineData(0.0, 0.0, 5.0)]
    [InlineData(1.0, 0.5, 10.0)]
    [InlineData(-2.0, -0.3, 20.0)]
    public void Project_LookAtTarget_AlwaysLandsAtViewportCenter(double yaw, double pitch, double distance)
    {
        var target = new Vector3(1, 2, 3);
        var camera = new OrbitCamera(target, yaw, pitch, distance);

        var projected = ViewportProjector.Project(target, camera, viewportWidth: 800, viewportHeight: 600);

        Assert.NotNull(projected);
        Assert.Equal(400.0, projected.Value.ScreenX, precision: 3);
        Assert.Equal(300.0, projected.Value.ScreenY, precision: 3);
    }

    [Fact]
    public void Project_PointBehindCamera_ReturnsNull()
    {
        var target = Vector3.Zero;
        var camera = new OrbitCamera(target, yaw: 0.0, pitch: 0.0, distance: 10.0);
        var eye = camera.GetEyePosition();

        // Twice as far behind the eye as the target is in front of it - guaranteed to be
        // on the wrong side of the camera to ever appear on screen.
        var pointBehindCamera = eye + (eye - target) * 2;

        var projected = ViewportProjector.Project(pointBehindCamera, camera, viewportWidth: 800, viewportHeight: 600);

        Assert.Null(projected);
    }

    [Fact]
    public void Project_FartherPoint_HasGreaterDepthThanNearerPoint()
    {
        var target = Vector3.Zero;
        var camera = new OrbitCamera(target, yaw: 0.0, pitch: 0.3, distance: 10.0);
        var nearPoint = new Vector3(0, 1, 0);
        var farPoint = new Vector3(0, -1, 0); // opposite side of the target from a yaw=0 camera

        var nearProjected = ViewportProjector.Project(nearPoint, camera, viewportWidth: 800, viewportHeight: 600);
        var farProjected = ViewportProjector.Project(farPoint, camera, viewportWidth: 800, viewportHeight: 600);

        Assert.NotNull(nearProjected);
        Assert.NotNull(farProjected);
        Assert.True(nearProjected.Value.Depth < farProjected.Value.Depth);
    }
}
