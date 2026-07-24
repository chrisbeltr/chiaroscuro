using Chiaroscuro.Core.Geometry;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class IlluminationPatchClipperTests
{
    // halfWidth = 3, halfLength = 2, height = 3 - shared by every test below.
    private static readonly Room TestRoom = new(Width: 6, Length: 4, Height: 3);

    [Fact]
    public void Clip_EntirelyWithinBounds_ReturnsSinglePatchMatchingTheInput()
    {
        Vector3[] projection =
        [
            new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
            new Vector3(1, 1, 0), new Vector3(-1, 1, 0),
        ];
        // Unused by this case (nothing overflows, so nothing gets re-projected), but every
        // projected corner still needs a paired "original corner" value.
        var corners = projection;

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.Floor, WallOrientation.West, corners, projection, lightDirection: new Vector3(0, 0, -1));

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.Floor, patch.Surface);
        Assert.Equal(projection, patch.Polygon);
    }

    [Fact]
    public void Clip_OverflowPastFloorEdge_WrapsOntoTheAdjacentWall()
    {
        // A window-shaped patch travelling along (0, -0.6, -0.8): its "top" edge (Y=-0.3)
        // lands inside the floor's Y bound (halfLength=2, landing Y=-1.8), but its "bottom"
        // edge (Y=-1.0) overflows past it (landing Y=-2.5) and should wrap onto the South wall.
        var lightDirection = new Vector3(0, -0.6, -0.8);
        Vector3[] corners =
        [
            new Vector3(-0.5, -1.0, 2.0), new Vector3(0.5, -1.0, 2.0),
            new Vector3(0.5, -0.3, 2.0), new Vector3(-0.5, -0.3, 2.0),
        ];
        Vector3[] projection =
        [
            new Vector3(-0.5, -2.5, 0), new Vector3(0.5, -2.5, 0),
            new Vector3(0.5, -1.8, 0), new Vector3(-0.5, -1.8, 0),
        ];

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.Floor, WallOrientation.West, corners, projection, lightDirection);

        Assert.Equal(2, patches.Count);

        var floorPatch = Assert.Single(patches, p => p.Surface == RoomSurface.Floor);
        Assert.All(floorPatch.Polygon, v => Assert.Equal(0.0, v.Z, precision: 9));
        Assert.All(floorPatch.Polygon, v => Assert.True(v.Y >= -2.0 - 1e-9 && v.Y <= -1.8 + 1e-9));
        Assert.Contains(floorPatch.Polygon, v => Math.Abs(v.Y - -2.0) < 1e-6);

        var southPatch = Assert.Single(patches, p => p.Surface == RoomSurface.SouthWall);
        Assert.All(southPatch.Polygon, v => Assert.Equal(-2.0, v.Y, precision: 6));
        Assert.Contains(southPatch.Polygon, v => Math.Abs(v.Z - 2.0 / 3.0) < 1e-4);
    }

    [Fact]
    public void Clip_OverflowPastAWallEdge_WrapsOntoTheAdjacentWallAcrossTheCorner()
    {
        // Same idea, but overflowing sideways off the North wall (past its +X edge,
        // halfWidth=3) onto the East wall instead - confirms the algorithm isn't floor-specific.
        var lightDirection = new Vector3(0.7071068, 0.7071068, 0);
        Vector3[] corners =
        [
            new Vector3(0.5, 1.5, 1.0), new Vector3(0.5, -1.0, 1.0),
            new Vector3(0.5, -1.0, 1.5), new Vector3(0.5, 1.5, 1.5),
        ];
        Vector3[] projection =
        [
            new Vector3(1.0, 2, 1.0), new Vector3(3.5, 2, 1.0),
            new Vector3(3.5, 2, 1.5), new Vector3(1.0, 2, 1.5),
        ];

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.NorthWall, WallOrientation.South, corners, projection, lightDirection);

        Assert.Equal(2, patches.Count);

        var northPatch = Assert.Single(patches, p => p.Surface == RoomSurface.NorthWall);
        Assert.All(northPatch.Polygon, v => Assert.Equal(2.0, v.Y, precision: 6));
        Assert.All(northPatch.Polygon, v => Assert.True(v.X <= 3.0 + 1e-6));

        var eastPatch = Assert.Single(patches, p => p.Surface == RoomSurface.EastWall);
        Assert.All(eastPatch.Polygon, v => Assert.Equal(3.0, v.X, precision: 6));
        Assert.Contains(eastPatch.Polygon, v => Math.Abs(v.Y - 1.5) < 1e-3);
    }

    [Fact]
    public void Clip_OverflowAboveWallHeight_IsDroppedSinceNoCeilingIsModeled()
    {
        Vector3[] projection =
        [
            new Vector3(0, -2, 1.0), new Vector3(1, -2, 1.0),
            new Vector3(1, -2, 4.0), new Vector3(0, -2, 4.0),
        ];
        var corners = projection; // unused: nothing here ever gets re-projected (no ceiling to wrap onto)

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.SouthWall, WallOrientation.North, corners, projection,
            lightDirection: new Vector3(0, 0.7071068, -0.7071068));

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.SouthWall, patch.Surface);
        Assert.All(patch.Polygon, v => Assert.True(v.Z <= 3.0 + 1e-9));
        Assert.Contains(patch.Polygon, v => Math.Abs(v.Z - 3.0) < 1e-6);
    }

    [Fact]
    public void Clip_TouchingButNotCrossingABoundary_DoesNotCreateAnAdjacentPatch()
    {
        Vector3[] projection =
        [
            new Vector3(-0.5, 1.0, 0), new Vector3(0.5, 1.0, 0),
            new Vector3(0.5, 2.0, 0), new Vector3(-0.5, 2.0, 0), // top edge sits exactly on the floor's Y bound
        ];
        var corners = projection; // unused: the polygon never actually crosses the boundary

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.Floor, WallOrientation.West, corners, projection, lightDirection: new Vector3(0, 0, -1));

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.Floor, patch.Surface);
        Assert.Equal(projection, patch.Polygon);
    }

    [Fact]
    public void Clip_WhenOverflowCannotReachItsNeighborsPlane_DropsItWithoutThrowing()
    {
        // Light travelling due south (0,-1,0) has no sideways drift at all, so a patch that
        // spills past the South wall's +X edge can never continue on to reach the East
        // wall's plane by travelling further along the same direction - that overflow
        // should simply be dropped, not throw.
        var lightDirection = new Vector3(0, -1, 0);
        Vector3[] corners =
        [
            new Vector3(2.0, -1.0, 1.0), new Vector3(4.0, -1.0, 1.0),
            new Vector3(4.0, -1.0, 1.5), new Vector3(2.0, -1.0, 1.5),
        ];
        Vector3[] projection =
        [
            new Vector3(2.0, -2, 1.0), new Vector3(4.0, -2, 1.0),
            new Vector3(4.0, -2, 1.5), new Vector3(2.0, -2, 1.5),
        ];

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.SouthWall, WallOrientation.North, corners, projection, lightDirection);

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.SouthWall, patch.Surface);
        Assert.DoesNotContain(patches, p => p.Surface == RoomSurface.EastWall);
    }
}
