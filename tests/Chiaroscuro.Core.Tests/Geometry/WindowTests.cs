using Chiaroscuro.Core.Geometry;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class WindowTests
{
    private static readonly Room TestRoom = new(Width: 6, Length: 4, Height: 3);

    [Theory]
    [InlineData(WallOrientation.North, 0, 2)]
    [InlineData(WallOrientation.South, 0, -2)]
    [InlineData(WallOrientation.East, 3, 0)]
    [InlineData(WallOrientation.West, -3, 0)]
    public void GetCenter_PlacesWindowOnTheCorrectWallAtCorrectHeight(WallOrientation wall, double expectedX, double expectedY)
    {
        var window = new Window(wall, HorizontalOffset: 0, SillHeight: 1, Width: 1, Height: 1);

        var center = window.GetCenter(TestRoom);

        Assert.Equal(expectedX, center.X, precision: 9);
        Assert.Equal(expectedY, center.Y, precision: 9);
        Assert.Equal(1.5, center.Z, precision: 9); // SillHeight (1) + Height/2 (0.5)
    }

    [Fact]
    public void GetCorners_OnNorthWall_FormsRectangleAtCorrectSizeAndPosition()
    {
        // North wall's in-plane horizontal axis is X, so corners should vary in X and Z,
        // and all four should share the exact same Y (they all lie flush on the wall).
        var window = new Window(WallOrientation.North, HorizontalOffset: 0, SillHeight: 1, Width: 2, Height: 1);

        var corners = window.GetCorners(TestRoom);

        Assert.Equal(4, corners.Length);
        Assert.All(corners, corner => Assert.Equal(2.0, corner.Y, precision: 9)); // flush on the North wall (Length/2)

        var xs = corners.Select(c => c.X).ToArray();
        var zs = corners.Select(c => c.Z).ToArray();
        Assert.Equal(-1.0, xs.Min(), precision: 9); // HorizontalOffset(0) - Width/2(1)
        Assert.Equal(1.0, xs.Max(), precision: 9);  // HorizontalOffset(0) + Width/2(1)
        Assert.Equal(1.0, zs.Min(), precision: 9);  // center(1.5) - Height/2(0.5)
        Assert.Equal(2.0, zs.Max(), precision: 9);  // center(1.5) + Height/2(0.5)
    }

    [Fact]
    public void GetCorners_OnEastWall_VariesInYNotX()
    {
        // East/West walls run along Y, so here it's Y (not X) that should vary, and
        // every corner should share the same X (flush on the East wall).
        var window = new Window(WallOrientation.East, HorizontalOffset: 0.5, SillHeight: 0, Width: 2, Height: 2);

        var corners = window.GetCorners(TestRoom);

        Assert.All(corners, corner => Assert.Equal(3.0, corner.X, precision: 9)); // flush on the East wall (Width/2)

        var ys = corners.Select(c => c.Y).ToArray();
        Assert.Equal(-0.5, ys.Min(), precision: 9); // HorizontalOffset(0.5) - Width/2(1)
        Assert.Equal(1.5, ys.Max(), precision: 9);  // HorizontalOffset(0.5) + Width/2(1)
    }
}
