using Chiaroscuro.Core.Geometry;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class RayTests
{
    [Fact]
    public void PointAt_WalksAlongDirectionByDistanceT()
    {
        var ray = new Ray(Origin: new Vector3(1, 1, 1), Direction: new Vector3(0, 0, 1));

        Assert.Equal(new Vector3(1, 1, 4), ray.PointAt(3));
    }

    [Fact]
    public void IntersectParameter_ReturnsExpectedT_ForPerpendicularApproach()
    {
        // Ray starts 5 units above the XY plane (z=0), heading straight down (-Z).
        // It should reach the plane after travelling exactly 5 units.
        var ray = new Ray(Origin: new Vector3(0, 0, 5), Direction: new Vector3(0, 0, -1));
        var plane = new Plane(Point: Vector3.Zero, Normal: new Vector3(0, 0, 1));

        var t = ray.IntersectParameter(plane);

        Assert.NotNull(t);
        Assert.Equal(5.0, t.Value, precision: 9);
        Assert.Equal(Vector3.Zero, ray.PointAt(t.Value));
    }

    [Fact]
    public void IntersectParameter_ReturnsNull_WhenRayIsParallelToPlane()
    {
        // Ray travels along X, but the plane's normal is also along X-perpendicular Y -
        // i.e. the ray runs flat along the plane's surface and never crosses it.
        var ray = new Ray(Origin: new Vector3(0, 1, 0), Direction: new Vector3(1, 0, 0));
        var plane = new Plane(Point: Vector3.Zero, Normal: new Vector3(0, 1, 0));

        Assert.Null(ray.IntersectParameter(plane));
    }

    [Fact]
    public void IntersectParameter_ReturnsNull_WhenPlaneIsBehindRayOrigin()
    {
        // Ray at z=5 heading further up (+Z) away from the z=0 plane: the plane is
        // mathematically "behind" it (negative t), which isn't a valid forward hit.
        var ray = new Ray(Origin: new Vector3(0, 0, 5), Direction: new Vector3(0, 0, 1));
        var plane = new Plane(Point: Vector3.Zero, Normal: new Vector3(0, 0, 1));

        Assert.Null(ray.IntersectParameter(plane));
    }
}
