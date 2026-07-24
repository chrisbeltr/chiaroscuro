using Chiaroscuro.Core.Geometry;
using Chiaroscuro.UI.Viewport;
using Xunit;

namespace Chiaroscuro.UI.Tests.Viewport;

public class SceneBuilderTests
{
    private static readonly Room TestRoom = new(Width: 6, Length: 5, Height: 3);
    private static readonly Window TestWindow = new(WallOrientation.South, HorizontalOffset: 0, SillHeight: 1, Width: 1.2, Height: 1.5);

    private static readonly Vector3[] TestIlluminatedPolygon =
    [
        new Vector3(-0.6, -2.5, 0), new Vector3(0.6, -2.5, 0),
        new Vector3(0.6, -1.0, 0), new Vector3(-0.6, -1.0, 0),
    ];

    [Fact]
    public void Build_WithNoIllumination_OnlyEmitsWireframeLines()
    {
        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination: null);

        Assert.All(primitives, p => Assert.IsType<SceneLine>(p));
        // 12 room edges (4 floor + 4 ceiling + 4 vertical) + 4 window frame edges.
        Assert.Equal(16, primitives.Count);
    }

    [Fact]
    public void Build_WithIllumination_AlsoEmitsFourLightConeFacesAndOneLandingPatch()
    {
        var illumination = new IlluminationResult(RoomSurface.Floor, new Vector3(0, -1.75, 0), TestIlluminatedPolygon);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination);

        Assert.Equal(5, primitives.OfType<ScenePolygon>().Count()); // 4 light-cone faces + 1 landing patch
        Assert.Equal(16, primitives.OfType<SceneLine>().Count()); // wireframe is unaffected
    }

    [Fact]
    public void Build_LightConeFaces_ConnectMatchingWindowAndLandingCorners()
    {
        var windowCorners = TestWindow.GetCorners(TestRoom);
        var illumination = new IlluminationResult(RoomSurface.Floor, new Vector3(0, -1.75, 0), TestIlluminatedPolygon);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination);

        var firstFace = primitives.OfType<ScenePolygon>().First();
        Assert.Equal(
            new[] { windowCorners[0], windowCorners[1], TestIlluminatedPolygon[1], TestIlluminatedPolygon[0] },
            firstFace.Corners);
    }
}
