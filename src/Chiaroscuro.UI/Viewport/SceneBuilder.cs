using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.UI.Viewport;

/// <summary>Builds the flat list of world-space <see cref="ScenePrimitive"/>s for a given
/// room/window/illumination state - the "what to draw" step, independent of "how to draw
/// it" (that's <see cref="Views.RoomViewport"/>'s job).</summary>
public static class SceneBuilder
{
    // Matches ChiaroscuroTheme.axaml's palette: ChiaroscuroForegroundColor for wireframe,
    // ChiaroscuroAmberColor/ChiaroscuroSunYellowColor (with transparency) for light.
    private static readonly SceneColor WireframeColor = new(0x94, 0x91, 0xC0);
    private static readonly SceneColor LightConeColor = new(0xF5, 0x9E, 0x0B, 60);
    private static readonly SceneColor LandingPatchColor = new(0xFD, 0xE0, 0x47, 140);

    public static IReadOnlyList<ScenePrimitive> Build(Room room, Window window, IlluminationResult? illumination)
    {
        var primitives = new List<ScenePrimitive>();

        AddRoomWireframe(primitives, room);
        AddRectangleEdges(primitives, window.GetCorners(room), WireframeColor);

        if (illumination is { } hit)
        {
            AddLightCone(primitives, window.GetCorners(room), hit.IlluminatedPolygon);
            primitives.Add(new ScenePolygon(hit.IlluminatedPolygon, LandingPatchColor));
        }

        return primitives;
    }

    private static void AddRoomWireframe(List<ScenePrimitive> primitives, Room room)
    {
        var halfWidth = room.Width / 2;
        var halfLength = room.Length / 2;
        var height = room.Height;

        Vector3[] floor =
        [
            new(-halfWidth, -halfLength, 0), new(halfWidth, -halfLength, 0),
            new(halfWidth, halfLength, 0), new(-halfWidth, halfLength, 0),
        ];
        Vector3[] ceiling =
        [
            new(-halfWidth, -halfLength, height), new(halfWidth, -halfLength, height),
            new(halfWidth, halfLength, height), new(-halfWidth, halfLength, height),
        ];

        AddRectangleEdges(primitives, floor, WireframeColor);
        AddRectangleEdges(primitives, ceiling, WireframeColor);

        // Vertical edges connecting each floor corner to the ceiling corner above it.
        for (var i = 0; i < 4; i++)
        {
            primitives.Add(new SceneLine(floor[i], ceiling[i], WireframeColor));
        }
    }

    private static void AddRectangleEdges(List<ScenePrimitive> primitives, Vector3[] corners, SceneColor color)
    {
        for (var i = 0; i < corners.Length; i++)
        {
            primitives.Add(new SceneLine(corners[i], corners[(i + 1) % corners.Length], color));
        }
    }

    /// <summary>The four translucent quad faces connecting each window corner to the
    /// matching corner of where its light lands - together forming the light "cone" (really
    /// a frustum, since both ends are rectangles). Relies on <see cref="Window.GetCorners"/>
    /// and <see cref="IlluminationResult.IlluminatedPolygon"/> sharing the same corner
    /// ordering (bottom-left, bottom-right, top-right, top-left) - see RayTracer.cs, which
    /// projects each window corner in order to build IlluminatedPolygon.</summary>
    private static void AddLightCone(List<ScenePrimitive> primitives, Vector3[] windowCorners, Vector3[] landingCorners)
    {
        for (var i = 0; i < windowCorners.Length; i++)
        {
            var next = (i + 1) % windowCorners.Length;
            primitives.Add(new ScenePolygon(
                [windowCorners[i], windowCorners[next], landingCorners[next], landingCorners[i]],
                LightConeColor));
        }
    }
}
