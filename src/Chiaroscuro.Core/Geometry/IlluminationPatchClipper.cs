namespace Chiaroscuro.Core.Geometry;

/// <summary>
/// Fixes a gap in <see cref="RayTracer"/>'s raw projection: projecting all four window
/// corners onto a single target plane only checks that the ray's *center* point stays
/// within that surface's physical bounds (see <see cref="Room.IsWithinSurfaceBounds"/>) -
/// the other three corners aren't checked, so a light patch can extend past the edge of
/// the wall/floor it's nominally drawn on.
/// <para>
/// This class fixes that by clipping the projected polygon against the target surface's
/// real edges, and - for whatever gets clipped away - continuing the *same* light ray
/// onto whichever neighboring surface it would actually land on next (floor to a wall,
/// or wall to an adjacent wall around a room corner). The result is one or more
/// <see cref="LandingPatch"/>es that together cover the true shape of the light, each
/// strictly within its own surface's bounds.
/// </para>
/// </summary>
public static class IlluminationPatchClipper
{
    /// <summary>
    /// A vertex of the polygon being clipped, together with which original window corner
    /// (if any) it came from. That link is what lets a clipped-away vertex be re-projected
    /// onto a neighboring surface: re-project the *original* corner along the light
    /// direction, not the already-projected point (which only exists on the surface being
    /// left behind). Vertices inserted by clipping itself - where the polygon crosses a
    /// surface's edge - have no original corner (null): they already sit exactly on the
    /// line shared by both surfaces, so they're valid on either one without re-projecting.
    /// </summary>
    private readonly record struct PatchVertex(Vector3 Position, Vector3? OriginalWindowCorner);

    /// <summary>One physical edge of a room surface's rectangular extent: a test for which
    /// side of that edge a point falls on, and which surface (if any) lies beyond it.</summary>
    private readonly record struct BoundaryEdge(Func<Vector3, double> SignedInsideDistance, RoomSurface? Neighbor);

    /// <summary>
    /// Clips the window's projected corners against <paramref name="primarySurface"/>'s real
    /// bounds, continuing any overflow onto whichever neighboring surfaces it actually lands
    /// on, however many hops that takes.
    /// </summary>
    /// <param name="room">The room, for surface planes/bounds.</param>
    /// <param name="primarySurface">The surface <paramref name="primaryProjection"/> was projected onto - typically <c>RayTracer</c>'s center-ray hit.</param>
    /// <param name="windowWall">The window's own wall - light can never land back on it, so it's excluded from every candidate surface, matching <see cref="Room.GetCandidateSurfaces"/>.</param>
    /// <param name="windowCorners">The window's own four corners in room space (pre-projection), in the same order as <paramref name="primaryProjection"/>.</param>
    /// <param name="primaryProjection">Each of <paramref name="windowCorners"/> already projected onto <paramref name="primarySurface"/>'s plane (e.g. <c>RayTracer</c>'s <c>IlluminatedPolygon</c>).</param>
    /// <param name="lightDirection">The direction light travels, shared by every corner.</param>
    public static IReadOnlyList<LandingPatch> Clip(
        Room room,
        RoomSurface primarySurface,
        WallOrientation windowWall,
        Vector3[] windowCorners,
        Vector3[] primaryProjection,
        Vector3 lightDirection)
    {
        var initialVertices = new List<PatchVertex>(windowCorners.Length);
        for (var i = 0; i < windowCorners.Length; i++)
        {
            initialVertices.Add(new PatchVertex(primaryProjection[i], windowCorners[i]));
        }

        var patches = new List<LandingPatch>();
        // Seeding the visited set with the window's own wall stops the recursion from ever
        // trying to wrap light back onto the surface it came through, and (together with
        // marking each surface visited as we enter it below) guarantees termination: there
        // are only 5 surfaces total, and each recursive call consumes exactly one.
        var visited = new HashSet<RoomSurface> { Room.ToRoomSurface(windowWall) };

        ClipOntoSurface(room, primarySurface, lightDirection, initialVertices, visited, patches);

        return patches;
    }

    private static void ClipOntoSurface(
        Room room,
        RoomSurface surface,
        Vector3 lightDirection,
        IReadOnlyList<PatchVertex> polygon,
        HashSet<RoomSurface> visited,
        List<LandingPatch> patches)
    {
        visited.Add(surface);
        var remaining = polygon;

        foreach (var edge in GetBoundaryEdges(room, surface))
        {
            var (inside, outside) = SplitBySignedDistance(remaining, edge.SignedInsideDistance);
            remaining = inside;

            if (outside.Count < 3 || edge.Neighbor is not { } neighbor || visited.Contains(neighbor))
            {
                // Nothing spilled past this edge, there's no surface beyond it (e.g. above a
                // wall's top - this app doesn't model a ceiling), or we've already visited
                // it earlier in this recursion - either way, that overflow is simply lost,
                // the same way RayTracer.Trace already treats "no valid hit" as normal.
                continue;
            }

            if (TryReproject(outside, lightDirection, room.GetPlane(neighbor)) is { } reprojected)
            {
                ClipOntoSurface(room, neighbor, lightDirection, reprojected, visited, patches);
            }
            // If TryReproject returns null, the light direction runs parallel to the
            // neighboring surface's plane (a ray travelling, say, due south can never also
            // reach an east/west wall by continuing further) - that overflow is dropped,
            // same as the no-neighbor case above.
        }

        if (remaining.Count >= 3)
        {
            patches.Add(new LandingPatch(surface, remaining.Select(vertex => vertex.Position).ToArray()));
        }
    }

    /// <summary>Re-projects every corner-derived vertex in <paramref name="outside"/> onto
    /// <paramref name="neighborPlane"/>, leaving boundary-crossing vertices (no original
    /// corner) untouched since they already lie on it. Returns null if any corner's ray
    /// turns out to run parallel to the neighbor's plane, since that leaves the whole
    /// group without a valid position on it.</summary>
    private static List<PatchVertex>? TryReproject(IReadOnlyList<PatchVertex> outside, Vector3 lightDirection, Plane neighborPlane)
    {
        var reprojected = new List<PatchVertex>(outside.Count);

        foreach (var vertex in outside)
        {
            if (vertex.OriginalWindowCorner is not { } corner)
            {
                reprojected.Add(vertex);
                continue;
            }

            if (RayTracer.TryProjectOntoPlane(corner, lightDirection, neighborPlane) is not { } projected)
            {
                return null;
            }

            reprojected.Add(new PatchVertex(projected, corner));
        }

        return reprojected;
    }

    /// <summary>
    /// Splits a convex polygon into the part that satisfies <paramref name="signedInsideDistance"/>
    /// (>= 0) and the part that doesn't, inserting the exact crossing point into both halves
    /// wherever an edge of the polygon crosses from one side to the other (standard
    /// Sutherland-Hodgman clipping). The polygon stays convex through repeated calls to this
    /// method, since intersecting a convex shape with a half-plane is always convex.
    /// </summary>
    private static (List<PatchVertex> Inside, List<PatchVertex> Outside) SplitBySignedDistance(
        IReadOnlyList<PatchVertex> polygon, Func<Vector3, double> signedInsideDistance)
    {
        var inside = new List<PatchVertex>();
        var outside = new List<PatchVertex>();

        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            var currentDistance = signedInsideDistance(current.Position);
            var nextDistance = signedInsideDistance(next.Position);

            (currentDistance >= 0 ? inside : outside).Add(current);

            if (currentDistance * nextDistance < 0)
            {
                // The edge from current to next crosses the boundary - find exactly where
                // (signed distance is linear along a straight edge, so this is a plain
                // linear interpolation) and give that point to both halves, since it
                // belongs to whichever surface is being considered on either side of it.
                var t = currentDistance / (currentDistance - nextDistance);
                var crossingPosition = current.Position + (next.Position - current.Position) * t;
                var crossingVertex = new PatchVertex(crossingPosition, OriginalWindowCorner: null);
                inside.Add(crossingVertex);
                outside.Add(crossingVertex);
            }
        }

        return (inside, outside);
    }

    /// <summary>
    /// The (up to) four physical edges of <paramref name="surface"/>'s rectangular extent,
    /// each paired with whichever surface borders it - mirrors
    /// <see cref="Room.IsWithinSurfaceBounds"/>'s per-surface bounds checks, but exposes
    /// *which* neighbor lies beyond each specific edge instead of a single in/out bool.
    /// </summary>
    private static IReadOnlyList<BoundaryEdge> GetBoundaryEdges(Room room, RoomSurface surface)
    {
        var halfWidth = room.Width / 2;
        var halfLength = room.Length / 2;
        var height = room.Height;

        return surface switch
        {
            RoomSurface.Floor =>
            [
                new BoundaryEdge(p => halfWidth - p.X, RoomSurface.EastWall),
                new BoundaryEdge(p => p.X + halfWidth, RoomSurface.WestWall),
                new BoundaryEdge(p => halfLength - p.Y, RoomSurface.NorthWall),
                new BoundaryEdge(p => p.Y + halfLength, RoomSurface.SouthWall),
            ],
            RoomSurface.NorthWall or RoomSurface.SouthWall =>
            [
                new BoundaryEdge(p => halfWidth - p.X, RoomSurface.EastWall),
                new BoundaryEdge(p => p.X + halfWidth, RoomSurface.WestWall),
                new BoundaryEdge(p => height - p.Z, null), // above the wall's top: no ceiling is modeled
                new BoundaryEdge(p => p.Z, RoomSurface.Floor),
            ],
            RoomSurface.EastWall or RoomSurface.WestWall =>
            [
                new BoundaryEdge(p => halfLength - p.Y, RoomSurface.NorthWall),
                new BoundaryEdge(p => p.Y + halfLength, RoomSurface.SouthWall),
                new BoundaryEdge(p => height - p.Z, null),
                new BoundaryEdge(p => p.Z, RoomSurface.Floor),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(surface)),
        };
    }
}
