using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.UI.Viewport;

/// <summary>
/// Clips a polygon down to whatever portion lies within a room's physical box - used to
/// keep purely-visual primitives (the light cone's side faces) from being drawn outside
/// the room's walls/floor/ceiling.
/// <para>
/// This is a rendering-only concern, deliberately separate from
/// <see cref="Chiaroscuro.Core.Geometry.IlluminationPatchClipper"/>: that one re-projects
/// overflow onto whichever surface the light physically continues onto, because a landing
/// patch is a real, located thing. The cone isn't - it's a loose translucent abstraction of
/// the light volume, so there's nothing physically meaningful to re-project a cone face onto
/// once it extends past a wall. Whatever falls outside the room's box is simply discarded.
/// </para>
/// </summary>
public static class RoomBoundsClipper
{
    /// <summary>Clips <paramref name="polygon"/> against all 6 of the room's bounding planes
    /// (floor, ceiling, and 4 walls) and returns whatever remains inside. May return an empty
    /// array if the whole polygon falls outside the room.</summary>
    public static Vector3[] ClipToRoom(Vector3[] polygon, Room room)
    {
        var halfWidth = room.Width / 2;
        var halfLength = room.Length / 2;
        var height = room.Height;

        var clipped = polygon;
        clipped = ClipToHalfSpace(clipped, p => halfWidth - p.X);
        clipped = ClipToHalfSpace(clipped, p => p.X + halfWidth);
        clipped = ClipToHalfSpace(clipped, p => halfLength - p.Y);
        clipped = ClipToHalfSpace(clipped, p => p.Y + halfLength);
        clipped = ClipToHalfSpace(clipped, p => height - p.Z);
        clipped = ClipToHalfSpace(clipped, p => p.Z);

        return clipped;
    }

    /// <summary>Standard Sutherland-Hodgman clip of a convex polygon against one half-space:
    /// keeps every vertex with a non-negative <paramref name="insideDistance"/>, inserting the
    /// exact boundary-crossing point wherever an edge switches sides.</summary>
    private static Vector3[] ClipToHalfSpace(Vector3[] polygon, Func<Vector3, double> insideDistance)
    {
        if (polygon.Length == 0)
        {
            return polygon;
        }

        var result = new List<Vector3>();

        for (var i = 0; i < polygon.Length; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Length];
            var currentDistance = insideDistance(current);
            var nextDistance = insideDistance(next);

            if (currentDistance >= 0)
            {
                result.Add(current);
            }

            if (currentDistance * nextDistance < 0)
            {
                var t = currentDistance / (currentDistance - nextDistance);
                result.Add(current + (next - current) * t);
            }
        }

        return result.ToArray();
    }
}
