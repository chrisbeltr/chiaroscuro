using Chiaroscuro.Core.Geometry;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class RoomTests
{
    private static readonly Room TestRoom = new(Width: 6, Length: 4, Height: 3);

    [Fact]
    public void GetPlane_Floor_IsAtOriginFacingUp()
    {
        var plane = TestRoom.GetPlane(RoomSurface.Floor);

        Assert.Equal(Vector3.Zero, plane.Point);
        Assert.Equal(new Vector3(0, 0, 1), plane.Normal);
    }

    [Theory]
    [InlineData(RoomSurface.NorthWall, 0, 2, 0)]   // +Y = North, at half the room's Length
    [InlineData(RoomSurface.SouthWall, 0, -2, 0)]
    [InlineData(RoomSurface.EastWall, 3, 0, 0)]    // +X = East, at half the room's Width
    [InlineData(RoomSurface.WestWall, -3, 0, 0)]
    public void GetPlane_Wall_IsPositionedAtRoomBoundary(RoomSurface surface, double expectedX, double expectedY, double expectedZ)
    {
        var plane = TestRoom.GetPlane(surface);

        Assert.Equal(new Vector3(expectedX, expectedY, expectedZ), plane.Point);
    }

    [Theory]
    [InlineData(2.9, 1.9, true)]   // just inside both bounds
    [InlineData(3.1, 1.9, false)]  // outside Width/2
    [InlineData(2.9, 2.1, false)]  // outside Length/2
    public void IsWithinSurfaceBounds_Floor_ChecksWidthAndLengthExtent(double x, double y, bool expected)
    {
        Assert.Equal(expected, TestRoom.IsWithinSurfaceBounds(RoomSurface.Floor, new Vector3(x, y, 0)));
    }

    [Theory]
    [InlineData(0, 1.5, true)]   // within Height
    [InlineData(0, -0.1, false)] // below the floor
    [InlineData(0, 3.1, false)]  // above the ceiling
    public void IsWithinSurfaceBounds_Wall_ChecksHeightExtent(double x, double z, bool expected)
    {
        Assert.Equal(expected, TestRoom.IsWithinSurfaceBounds(RoomSurface.NorthWall, new Vector3(x, 2, z)));
    }

    [Theory]
    [InlineData(WallOrientation.North, RoomSurface.NorthWall)]
    [InlineData(WallOrientation.South, RoomSurface.SouthWall)]
    [InlineData(WallOrientation.East, RoomSurface.EastWall)]
    [InlineData(WallOrientation.West, RoomSurface.WestWall)]
    public void GetCandidateSurfaces_ExcludesOnlyTheWindowsOwnWall(WallOrientation windowWall, RoomSurface excludedSurface)
    {
        var candidates = TestRoom.GetCandidateSurfaces(windowWall).ToList();

        Assert.DoesNotContain(excludedSurface, candidates);
        Assert.Equal(4, candidates.Count); // 5 total surfaces minus the excluded one
    }
}
