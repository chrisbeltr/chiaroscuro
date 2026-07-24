namespace Chiaroscuro.Core.Geometry;

using Chiaroscuro.Core.Solar;

/// <summary>
/// The surface a sun ray lands on inside the room, plus the illuminated window-shaped
/// polygon projected onto that surface. Null from <see cref="RayTracer.Trace"/> means
/// the ray doesn't land inside the room at all (e.g. the sun is below the horizon, or
/// the geometry simply doesn't line up).
/// </summary>
/// <param name="Surface">Which room surface (floor or a wall) is illuminated.</param>
/// <param name="CenterPoint">Where the window's center ray lands on that surface.</param>
/// <param name="IlluminatedPolygon">
/// The window's four corners, each projected along the light direction onto the target
/// surface - the actual shape/position of the light patch (spec §3.2's "Aperture Projection").
/// This is the RAW, unclipped projection: it can extend past the target surface's real
/// physical bounds near a room edge/corner. It's kept exactly as-is (rather than clipped)
/// because <c>Chiaroscuro.UI</c>'s light-cone rendering relies on it always having exactly
/// 4 corners in the same order as <see cref="Window.GetCorners"/>. Use <see cref="Patches"/>
/// for the physically-accurate, in-bounds shape.
/// </param>
/// <param name="Patches">
/// The physically-accurate landing shape: one or more <see cref="LandingPatch"/>es whose
/// polygons together cover the same light patch as <see cref="IlluminatedPolygon"/>, but
/// each strictly clipped to its own surface's real bounds - any part that would spill past
/// a surface's edge is re-projected onto whichever neighboring surface it actually lands
/// on (see <see cref="IlluminationPatchClipper"/>). In the common case (no spillover) this
/// is a single entry shaped like <see cref="IlluminatedPolygon"/>.
/// </param>
public readonly record struct IlluminationResult(
    RoomSurface Surface,
    Vector3 CenterPoint,
    Vector3[] IlluminatedPolygon,
    IReadOnlyList<LandingPatch> Patches);

/// <summary>A single, physically-real piece of a light patch confined to one room surface.
/// See <see cref="IlluminationResult.Patches"/> and <see cref="IlluminationPatchClipper"/>.</summary>
/// <param name="Surface">Which surface this piece of the patch lies on.</param>
/// <param name="Polygon">
/// The patch's corners on that surface, in order. Unlike <see cref="IlluminationResult.IlluminatedPolygon"/>,
/// this isn't always 4 points - clipping a quad against a surface's edge can add extra
/// vertices wherever the polygon crosses the boundary.
/// </param>
public readonly record struct LandingPatch(RoomSurface Surface, Vector3[] Polygon);

/// <summary>Implements spec §3.2: ray-plane intersection and aperture projection.</summary>
public static class RayTracer
{
    // Ray parameters at or below this are treated as "at the ray's own origin", not a
    // genuine forward hit - guards against a surface being reported as hit at t≈0 due
    // to floating-point noise right where the window itself sits on its wall.
    private const double MinimumRayParameter = 1e-9;

    public static IlluminationResult? Trace(Room room, Window window, SolarPosition sunPosition)
    {
        // The sun unit vector points FROM the room TOWARD the sun. Light itself travels
        // the opposite way - into the room - which is exactly spec §3.2's -S_v.
        var lightDirection = -sunPosition.ToUnitVector();
        var centerRay = new Ray(window.GetCenter(room), lightDirection);

        if (FindNearestSurface(room, window.Wall, centerRay) is not { } nearestHit)
        {
            return null;
        }

        var targetPlane = room.GetPlane(nearestHit.Surface);
        var windowCorners = window.GetCorners(room);

        // Project every window corner along the same light direction onto the same
        // plane the center point landed on - together these four points form the
        // illuminated polygon spec §3.2 calls for.
        var illuminatedPolygon = windowCorners
            .Select(corner => ProjectOntoPlane(corner, lightDirection, targetPlane))
            .ToArray();

        // The raw projection above can spill past the target surface's real edges near a
        // room corner - IlluminationPatchClipper re-derives the physically-correct shape,
        // continuing any spillover onto whichever surface it actually lands on.
        var patches = IlluminationPatchClipper.Clip(
            room, nearestHit.Surface, window.Wall, windowCorners, illuminatedPolygon, lightDirection);

        return new IlluminationResult(nearestHit.Surface, nearestHit.Point, illuminatedPolygon, patches);
    }

    /// <summary>
    /// Tests the ray against every candidate surface (floor + the three non-window walls)
    /// and returns whichever valid hit is closest to the ray's origin - the first surface
    /// the light actually reaches, since a room is a closed box and light stops at the
    /// first thing it touches.
    /// </summary>
    private static (RoomSurface Surface, Vector3 Point)? FindNearestSurface(Room room, WallOrientation windowWall, Ray ray)
    {
        (RoomSurface Surface, Vector3 Point)? nearestHit = null;
        var nearestParameter = double.PositiveInfinity;

        foreach (var surface in room.GetCandidateSurfaces(windowWall))
        {
            var plane = room.GetPlane(surface);

            if (ray.IntersectParameter(plane) is not { } t || t < MinimumRayParameter)
            {
                continue; // ray is parallel to this surface, or the surface is behind the ray's origin
            }

            var point = ray.PointAt(t);
            if (!room.IsWithinSurfaceBounds(surface, point))
            {
                continue; // crosses the surface's infinite plane, but outside its physical extent
            }

            if (t < nearestParameter)
            {
                nearestParameter = t;
                nearestHit = (surface, point);
            }
        }

        return nearestHit;
    }

    private static Vector3 ProjectOntoPlane(Vector3 point, Vector3 direction, Plane plane)
    {
        var ray = new Ray(point, direction);

        // Every window corner sits on the same wall plane as the window's center and
        // travels along the same light direction toward the same target plane the
        // center already hit, so this should always succeed.
        if (ray.IntersectParameter(plane) is not { } t)
        {
            throw new InvalidOperationException("Window corner unexpectedly failed to project onto the illuminated surface's plane.");
        }

        return ray.PointAt(t);
    }

    /// <summary>
    /// Like <see cref="ProjectOntoPlane"/>, but returns null instead of throwing when
    /// <paramref name="direction"/> runs parallel to <paramref name="plane"/>. Used by
    /// <see cref="IlluminationPatchClipper"/> when continuing a ray onto a neighboring
    /// surface, where - unlike the original window-corner-onto-primary-surface projection -
    /// there's no guarantee the ray isn't parallel to that particular neighbor (e.g. a ray
    /// travelling due south can never reach an east/west wall by continuing further).
    /// </summary>
    internal static Vector3? TryProjectOntoPlane(Vector3 point, Vector3 direction, Plane plane)
    {
        var ray = new Ray(point, direction);
        return ray.IntersectParameter(plane) is { } t ? ray.PointAt(t) : null;
    }
}
