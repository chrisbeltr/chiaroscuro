using Chiaroscuro.Core.Geometry;
using Chiaroscuro.UI.Viewport;
using Xunit;

namespace Chiaroscuro.UI.Tests.Viewport;

public class RoomBoundsClipperTests
{
    // halfWidth = 3, halfLength = 2, height = 3.
    private static readonly Room TestRoom = new(Width: 6, Length: 4, Height: 3);

    [Fact]
    public void ClipToRoom_EntirelyWithinBounds_ReturnsThePolygonUnchanged()
    {
        Vector3[] polygon =
        [
            new Vector3(-1, -1, 1), new Vector3(1, -1, 1),
            new Vector3(1, 1, 1), new Vector3(-1, 1, 1),
        ];

        var result = RoomBoundsClipper.ClipToRoom(polygon, TestRoom);

        Assert.Equal(polygon, result);
    }

    [Fact]
    public void ClipToRoom_ExtendingPastAWall_IsTruncatedAtTheWall()
    {
        Vector3[] polygon =
        [
            new Vector3(2, -1, 1), new Vector3(4, -1, 1),
            new Vector3(4, 1, 1), new Vector3(2, 1, 1),
        ];

        var result = RoomBoundsClipper.ClipToRoom(polygon, TestRoom);

        Assert.All(result, v => Assert.True(v.X <= 3.0 + 1e-9));
        Assert.Contains(result, v => Math.Abs(v.X - 3.0) < 1e-9);
        Assert.Contains(result, v => Math.Abs(v.X - 2.0) < 1e-9);
    }

    [Fact]
    public void ClipToRoom_EntirelyOutsideBounds_ReturnsEmpty()
    {
        Vector3[] polygon =
        [
            new Vector3(4, -1, 1), new Vector3(5, -1, 1),
            new Vector3(5, 1, 1), new Vector3(4, 1, 1),
        ];

        var result = RoomBoundsClipper.ClipToRoom(polygon, TestRoom);

        Assert.Empty(result);
    }
}
