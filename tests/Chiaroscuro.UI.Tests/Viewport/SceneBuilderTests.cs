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

    private static readonly IReadOnlyList<LandingPatch> TestPatches =
    [
        new LandingPatch(RoomSurface.Floor, TestIlluminatedPolygon),
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
        var illumination = new IlluminationResult(RoomSurface.Floor, new Vector3(0, -1.75, 0), TestIlluminatedPolygon, TestPatches);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination);

        Assert.Equal(5, primitives.OfType<ScenePolygon>().Count()); // 4 light-cone faces + 1 landing patch
        Assert.Equal(16, primitives.OfType<SceneLine>().Count()); // wireframe is unaffected
    }

    [Fact]
    public void Build_LightConeFaces_ConnectMatchingWindowAndLandingCorners()
    {
        var windowCorners = TestWindow.GetCorners(TestRoom);
        var illumination = new IlluminationResult(RoomSurface.Floor, new Vector3(0, -1.75, 0), TestIlluminatedPolygon, TestPatches);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination);

        var firstFace = primitives.OfType<ScenePolygon>().First();
        Assert.Equal(
            new[] { windowCorners[0], windowCorners[1], TestIlluminatedPolygon[1], TestIlluminatedPolygon[0] },
            firstFace.Corners);
    }

    [Fact]
    public void Build_WithMultiplePatches_EmitsOneScenePolygonPerPatch()
    {
        IReadOnlyList<LandingPatch> twoPatches =
        [
            new LandingPatch(RoomSurface.Floor, TestIlluminatedPolygon),
            new LandingPatch(RoomSurface.SouthWall,
            [
                new Vector3(-0.6, -2.5, 0.5), new Vector3(0.6, -2.5, 0.5),
                new Vector3(0.6, -2.5, 1.0), new Vector3(-0.6, -2.5, 1.0),
            ]),
        ];
        var illumination = new IlluminationResult(RoomSurface.Floor, new Vector3(0, -1.75, 0), TestIlluminatedPolygon, twoPatches);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination);

        // 4 light-cone faces (unchanged, still built from IlluminatedPolygon) + 2 landing-patch
        // fills (one per patch, instead of the usual 1).
        Assert.Equal(6, primitives.OfType<ScenePolygon>().Count());
    }

    [Fact]
    public void Build_WhenLightConeExtendsPastTheRoom_ClipsEachFaceToTheRoomBounds()
    {
        // An IlluminatedPolygon reaching X=4 - past TestRoom's halfWidth of 3 - simulates the
        // raw, unclipped projection poking through the East wall the way it used to render.
        Vector3[] overflowingPolygon =
        [
            new Vector3(-0.6, -2.5, 0), new Vector3(4.0, -2.5, 0),
            new Vector3(4.0, -1.0, 0), new Vector3(-0.6, -1.0, 0),
        ];
        IReadOnlyList<LandingPatch> patches = [new LandingPatch(RoomSurface.Floor, overflowingPolygon)];
        var illumination = new IlluminationResult(RoomSurface.Floor, new Vector3(0, -1.75, 0), overflowingPolygon, patches);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination);

        // AddLightCone runs before the patches loop, so the first 4 ScenePolygons are the
        // cone faces (same ordering assumption the earlier tests in this file already rely on).
        var coneFaces = primitives.OfType<ScenePolygon>().Take(4).ToList();
        Assert.Equal(4, coneFaces.Count);
        Assert.All(coneFaces, face => Assert.All(face.Corners, v => Assert.True(v.X <= 3.0 + 1e-9)));
        Assert.Contains(coneFaces, face => face.Corners.Any(v => Math.Abs(v.X - 3.0) < 1e-9));
    }
}
