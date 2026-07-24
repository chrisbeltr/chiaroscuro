using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.Solar;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class RayTracerTests
{
    private static readonly Room TestRoom = new(Width: 5, Length: 4, Height: 3);

    [Fact]
    public void Trace_SunDirectlyOverhead_HitsFloorDirectlyBelowWindowCenter()
    {
        // Elevation 90 makes cos(elevation)=0, so the sun unit vector collapses to
        // straight up (0,0,1) regardless of azimuth - light travels straight down.
        var window = new Window(WallOrientation.North, HorizontalOffset: 0.5, SillHeight: 1, Width: 1, Height: 1);
        var sunPosition = new SolarPosition(ElevationDegrees: 90, AzimuthDegrees: 137); // azimuth is irrelevant here

        var result = RayTracer.Trace(TestRoom, window, sunPosition);

        Assert.NotNull(result);
        Assert.Equal(RoomSurface.Floor, result.Value.Surface);
        // Straight down means X and Y are unchanged from the window's center; only Z moves to 0.
        var windowCenter = window.GetCenter(TestRoom);
        Assert.Equal(windowCenter.X, result.Value.CenterPoint.X, precision: 9);
        Assert.Equal(windowCenter.Y, result.Value.CenterPoint.Y, precision: 9);
        Assert.Equal(0.0, result.Value.CenterPoint.Z, precision: 9);
    }

    [Fact]
    public void Trace_GrazingLowSunAngle_HitsOppositeWallInsteadOfFloor()
    {
        // A low elevation (5 degrees) pointed so light travels mostly horizontally across
        // the room means it reaches the far (South) wall long before it would ever reach
        // the floor - see the derivation in this test's accompanying notes below.
        var window = new Window(WallOrientation.North, HorizontalOffset: 0, SillHeight: 1, Width: 1, Height: 1);
        var sunPosition = new SolarPosition(ElevationDegrees: 5, AzimuthDegrees: 0); // sun due North, low in the sky

        var result = RayTracer.Trace(TestRoom, window, sunPosition);

        Assert.NotNull(result);
        Assert.Equal(RoomSurface.SouthWall, result.Value.Surface);

        // Hand-derived expected hit point: light direction is (0, -cos5deg, -sin5deg).
        // Travelling from Y=+2 (window center, North wall) to Y=-2 (South wall) takes
        // t = 4 / cos(5deg) =~ 4.0153, during which Z drops from 1.5 by t * sin(5deg).
        var t = 4.0 / Math.Cos(double.DegreesToRadians(5));
        var expectedZ = 1.5 - t * Math.Sin(double.DegreesToRadians(5));
        Assert.Equal(-2.0, result.Value.CenterPoint.Y, precision: 6);
        Assert.Equal(expectedZ, result.Value.CenterPoint.Z, precision: 6);
    }

    [Fact]
    public void Trace_IlluminatedPolygon_HasFourCornersOnTheTargetSurfacePlane()
    {
        var window = new Window(WallOrientation.North, HorizontalOffset: 0, SillHeight: 1, Width: 1, Height: 1);
        var sunPosition = new SolarPosition(ElevationDegrees: 5, AzimuthDegrees: 0);

        var result = RayTracer.Trace(TestRoom, window, sunPosition);

        Assert.NotNull(result);
        Assert.Equal(4, result.Value.IlluminatedPolygon.Length);
        // Every projected corner must land exactly on the target surface's plane (Y=-2 for
        // the South wall) - if the projection math were wrong, this is the check that would catch it.
        Assert.All(result.Value.IlluminatedPolygon, corner => Assert.Equal(-2.0, corner.Y, precision: 6));
        // The polygon should not be degenerate: since this wall's light direction has no
        // X component, the window's Width should carry through unchanged into the polygon's X spread.
        var xSpread = result.Value.IlluminatedPolygon.Max(c => c.X) - result.Value.IlluminatedPolygon.Min(c => c.X);
        Assert.Equal(window.Width, xSpread, precision: 6);
    }

    [Fact]
    public void Trace_WhenLightOverflowsPastThePrimarySurface_PopulatesMultiplePatches()
    {
        // A narrow room and a wide, off-center East window pushes part of the light patch
        // past the West wall's own Y bound - RayTracer.Trace should wrap that overflow onto
        // the North wall rather than leaving it in the raw, unclipped IlluminatedPolygon.
        var room = new Room(Width: 4, Length: 2, Height: 3);
        var window = new Window(WallOrientation.East, HorizontalOffset: 0, SillHeight: 1, Width: 1.5, Height: 1);
        var sunPosition = new SolarPosition(ElevationDegrees: 5, AzimuthDegrees: 95);

        var result = RayTracer.Trace(room, window, sunPosition);

        Assert.NotNull(result);
        Assert.Equal(RoomSurface.WestWall, result.Value.Surface);
        Assert.Equal(2, result.Value.Patches.Count);

        var westPatch = Assert.Single(result.Value.Patches, p => p.Surface == RoomSurface.WestWall);
        Assert.All(westPatch.Polygon, v => Assert.True(v.Y <= 1.0 + 1e-6));

        var northPatch = Assert.Single(result.Value.Patches, p => p.Surface == RoomSurface.NorthWall);
        Assert.All(northPatch.Polygon, v => Assert.Equal(1.0, v.Y, precision: 6));
    }

    [Fact]
    public void Trace_ReturnsNull_WhenLightTravelsAwayFromTheRoomInterior()
    {
        // Sun below the horizon (negative elevation), positioned so -S_v points back out
        // through the window's own wall rather than into the room - no surface can be hit.
        var window = new Window(WallOrientation.North, HorizontalOffset: 0, SillHeight: 1, Width: 1, Height: 1);
        var sunPosition = new SolarPosition(ElevationDegrees: -5, AzimuthDegrees: 180);

        var result = RayTracer.Trace(TestRoom, window, sunPosition);

        Assert.Null(result);
    }
}
