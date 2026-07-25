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

    public static IReadOnlyList<ScenePrimitive> Build(
        Room room, Window window, IlluminationResult? illumination, Vector3? target = null, double? toleranceDegrees = null)
    {
        var primitives = new List<ScenePrimitive>();

        AddRoomWireframe(primitives, room);
        AddRectangleEdges(primitives, window.GetCorners(room), WireframeColor);

        if (illumination is { } hit)
        {
            // The cone still starts from the raw, unclipped projection rather than
            // following the fill's per-surface wrap - but each resulting face is now
            // clipped to the room's overall box so it never visually pokes through a
            // wall/floor/ceiling.
            AddLightCone(primitives, room, window.GetCorners(room), hit.IlluminatedPolygon);

            foreach (var patch in hit.Patches)
            {
                primitives.Add(new ScenePolygon(patch.Polygon, LandingPatchColor));
            }
        }

        if (target is { } targetPoint)
        {
            AddTargetIndicator(primitives, window.GetCenter(room), targetPoint, toleranceDegrees);
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

    /// <summary>Up to four translucent quad faces connecting each window corner to the
    /// matching corner of where its light lands - together forming the light "cone" (really
    /// a frustum, since both ends are rectangles). Relies on <see cref="Window.GetCorners"/>
    /// and <see cref="IlluminationResult.IlluminatedPolygon"/> sharing the same corner
    /// ordering (bottom-left, bottom-right, top-right, top-left) - see RayTracer.cs, which
    /// projects each window corner in order to build IlluminatedPolygon. Each face is clipped
    /// to <paramref name="room"/>'s box before being added, so a face never renders outside
    /// the physical room even though its source corners might extend past it; a face clipped
    /// away entirely is simply skipped.</summary>
    private static void AddLightCone(List<ScenePrimitive> primitives, Room room, Vector3[] windowCorners, Vector3[] landingCorners)
    {
        for (var i = 0; i < windowCorners.Length; i++)
        {
            var next = (i + 1) % windowCorners.Length;
            var face = RoomBoundsClipper.ClipToRoom(
                [windowCorners[i], windowCorners[next], landingCorners[next], landingCorners[i]], room);

            if (face.Length >= 3)
            {
                primitives.Add(new ScenePolygon(face, LightConeColor));
            }
        }
    }

    /// <summary>A small crosshair at <paramref name="target"/>, plus - if
    /// <paramref name="toleranceDegrees"/> is given - a ring around it showing how far off the
    /// sun's direction could be and still count as a match. The ring's radius is the angular
    /// tolerance converted to a spatial distance at the target's depth
    /// (<c>distance × tan(tolerance)</c>), and it lies in the plane perpendicular to the
    /// window→target direction - a reticle facing the window - rather than flattened onto
    /// whichever room surface happens to be nearby: the target isn't guaranteed to sit exactly
    /// on one, and perpendicular-to-the-ray is also the mathematically exact slice of the
    /// angular tolerance cone (anywhere else it would generally be an ellipse, not a circle).
    /// Both are drawn in the wireframe's own color, not amber/gold - they're measurement
    /// overlays, not light.</summary>
    private static void AddTargetIndicator(List<ScenePrimitive> primitives, Vector3 windowCenter, Vector3 target, double? toleranceDegrees)
    {
        var toWindow = windowCenter - target;
        if (toWindow.Length < 1e-9)
        {
            return; // target sits exactly on the window's center - no well-defined direction to build a basis from
        }

        var direction = toWindow.Normalized();

        // Any vector not parallel to `direction` works as a seed for building an orthonormal
        // in-plane basis - (0,0,1) works unless direction is itself nearly vertical, in which
        // case (1,0,0) is used instead.
        var seed = Math.Abs(direction.Z) > 0.99 ? new Vector3(1, 0, 0) : new Vector3(0, 0, 1);
        var right = direction.Cross(seed).Normalized();
        var up = right.Cross(direction).Normalized();

        const double crosshairArmLength = 0.1;
        primitives.Add(new SceneLine(target - right * crosshairArmLength, target + right * crosshairArmLength, WireframeColor));
        primitives.Add(new SceneLine(target - up * crosshairArmLength, target + up * crosshairArmLength, WireframeColor));

        if (toleranceDegrees is { } tolerance)
        {
            var radius = toWindow.Length * Math.Tan(double.DegreesToRadians(tolerance));
            const int segments = 32;
            var ringPoints = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2 * Math.PI * i / segments;
                ringPoints[i] = target + right * (radius * Math.Cos(angle)) + up * (radius * Math.Sin(angle));
            }

            for (var i = 0; i < segments; i++)
            {
                primitives.Add(new SceneLine(ringPoints[i], ringPoints[(i + 1) % segments], WireframeColor));
            }
        }
    }
}
