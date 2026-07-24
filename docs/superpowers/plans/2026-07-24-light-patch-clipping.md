# Light Patch Boundary Clipping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop the 3D viewport's light landing patch from extending past the physical wall/floor it's drawn on - clip it to that surface's real bounds and continue the same light ray onto whichever neighboring surface the overflow actually lands on.

**Architecture:** One new pure-geometry class, `Chiaroscuro.Core.Geometry.IlluminationPatchClipper`, implements a recursive Sutherland-Hodgman clip-and-continue algorithm: clip the projected window quad against a surface's 4 real edges, and for whatever's clipped away, re-project the underlying window corner onto whichever surface borders that specific edge, then recurse. `RayTracer.Trace` calls it and adds the result as a new `Patches` list on `IlluminationResult`, alongside the existing unclipped `IlluminatedPolygon` (kept as-is - it still feeds `SceneBuilder`'s light-cone side faces, which aren't changing in this pass). `SceneBuilder` draws one filled polygon per patch instead of one.

**Tech Stack:** No new dependencies - pure C# geometry in `Chiaroscuro.Core`, reusing the existing `Vector3`/`Plane`/`Ray`/`Room` types and xUnit test setup.

## Global Constraints

- Target framework `net10.0`, `Nullable` enabled, `LangVersion` 13 - inherited automatically from `Directory.Build.props`, nothing to configure per-project.
- No new failure modes (per `docs/superpowers/specs/2026-07-24-light-patch-clipping-design.md`): `RayTracer.Trace` still returns `null` under exactly the same conditions as today. Within a non-null result, any overflow that can't be resolved (no neighboring surface, e.g. above a wall's unmodeled ceiling; or a light direction that runs parallel to a neighbor's plane) is silently dropped, never thrown as an exception.
- `IlluminatedPolygon` keeps its exact current meaning, value, and computation - it is not touched by this plan.
- Every task must leave the whole solution (`Chiaroscuro.Core.Tests` + `Chiaroscuro.UI.Tests`) building and green - no task may end with a compile error in a sibling project.

---

### Task 1: `IlluminationPatchClipper`

**Files:**
- Modify: `src/Chiaroscuro.Core/Geometry/Room.cs` (widen `ToRoomSurface`'s access)
- Modify: `src/Chiaroscuro.Core/Geometry/RayTracer.cs` (add `TryProjectOntoPlane`)
- Create: `src/Chiaroscuro.Core/Geometry/IlluminationPatchClipper.cs`
- Test: `tests/Chiaroscuro.Core.Tests/Geometry/IlluminationPatchClipperTests.cs`

**Interfaces:**
- Consumes: `Chiaroscuro.Core.Geometry.Room` (`Width`, `Length`, `Height`, `GetPlane(RoomSurface)`, existing), `Vector3`, `Plane`, `Ray`, `RoomSurface`, `WallOrientation` (all existing).
- Produces:
  - `Room.ToRoomSurface(WallOrientation wall) -> RoomSurface` (now `internal`, was `private`).
  - `RayTracer.TryProjectOntoPlane(Vector3 point, Vector3 direction, Plane plane) -> Vector3?` (new, `internal`).
  - `IlluminationPatchClipper.Clip(Room room, RoomSurface primarySurface, WallOrientation windowWall, Vector3[] windowCorners, Vector3[] primaryProjection, Vector3 lightDirection) -> IReadOnlyList<LandingPatch>` (new, `public`).
  - `LandingPatch(RoomSurface Surface, Vector3[] Polygon)` (new record, added to `RayTracer.cs` in Step 3 below - needed as `Clip`'s return element type). Task 2 later adds a `Patches` field of this type to `IlluminationResult`, but this task doesn't touch `IlluminationResult` at all - it's purely additive.

This task is fully additive - it doesn't touch `RayTracer.Trace`, `IlluminationResult`'s existing fields, or `SceneBuilder`, so nothing downstream can break.

- [ ] **Step 1: Widen `Room.ToRoomSurface`'s access**

In `src/Chiaroscuro.Core/Geometry/Room.cs`, change:

```csharp
    private static RoomSurface ToRoomSurface(WallOrientation wall) => wall switch
    {
        WallOrientation.North => RoomSurface.NorthWall,
        WallOrientation.South => RoomSurface.SouthWall,
        WallOrientation.East => RoomSurface.EastWall,
        WallOrientation.West => RoomSurface.WestWall,
        _ => throw new ArgumentOutOfRangeException(nameof(wall)),
    };
```

to:

```csharp
    /// <summary>Maps a window's wall to the corresponding <see cref="RoomSurface"/> - e.g. for
    /// excluding a window's own wall from candidate landing surfaces. Internal (not private)
    /// so <see cref="IlluminationPatchClipper"/> can reuse it too.</summary>
    internal static RoomSurface ToRoomSurface(WallOrientation wall) => wall switch
    {
        WallOrientation.North => RoomSurface.NorthWall,
        WallOrientation.South => RoomSurface.SouthWall,
        WallOrientation.East => RoomSurface.EastWall,
        WallOrientation.West => RoomSurface.WestWall,
        _ => throw new ArgumentOutOfRangeException(nameof(wall)),
    };
```

(Only the access modifier and the doc comment change - the switch body is identical.)

- [ ] **Step 2: Add `RayTracer.TryProjectOntoPlane`**

In `src/Chiaroscuro.Core/Geometry/RayTracer.cs`, add this method to the `RayTracer` class, right after the existing `ProjectOntoPlane` method (leave `ProjectOntoPlane` itself completely unchanged - it's still used exactly as before for the primary-surface projection, where a parallel direction is provably impossible):

```csharp
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
```

- [ ] **Step 3: Add the `LandingPatch` record**

In `src/Chiaroscuro.Core/Geometry/RayTracer.cs`, add this record right after the existing `IlluminationResult` record (before the `RayTracer` class):

```csharp
/// <summary>A single, physically-real piece of a light patch confined to one room surface.
/// See <see cref="IlluminationResult.Patches"/> (added in a later step) and
/// <see cref="IlluminationPatchClipper"/>.</summary>
/// <param name="Surface">Which surface this piece of the patch lies on.</param>
/// <param name="Polygon">
/// The patch's corners on that surface, in order. Unlike <see cref="IlluminationResult.IlluminatedPolygon"/>,
/// this isn't always 4 points - clipping a quad against a surface's edge can add extra
/// vertices wherever the polygon crosses the boundary.
/// </param>
public readonly record struct LandingPatch(RoomSurface Surface, Vector3[] Polygon);
```

- [ ] **Step 4: Write the failing tests**

Create `tests/Chiaroscuro.Core.Tests/Geometry/IlluminationPatchClipperTests.cs`:

```csharp
using Chiaroscuro.Core.Geometry;
using Xunit;

namespace Chiaroscuro.Core.Tests.Geometry;

public class IlluminationPatchClipperTests
{
    // halfWidth = 3, halfLength = 2, height = 3 - shared by every test below.
    private static readonly Room TestRoom = new(Width: 6, Length: 4, Height: 3);

    [Fact]
    public void Clip_EntirelyWithinBounds_ReturnsSinglePatchMatchingTheInput()
    {
        Vector3[] projection =
        [
            new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
            new Vector3(1, 1, 0), new Vector3(-1, 1, 0),
        ];
        // Unused by this case (nothing overflows, so nothing gets re-projected), but every
        // projected corner still needs a paired "original corner" value.
        var corners = projection;

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.Floor, WallOrientation.West, corners, projection, lightDirection: new Vector3(0, 0, -1));

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.Floor, patch.Surface);
        Assert.Equal(projection, patch.Polygon);
    }

    [Fact]
    public void Clip_OverflowPastFloorEdge_WrapsOntoTheAdjacentWall()
    {
        // A window-shaped patch travelling along (0, -0.6, -0.8): its "top" edge (Y=-0.3)
        // lands inside the floor's Y bound (halfLength=2, landing Y=-1.8), but its "bottom"
        // edge (Y=-1.0) overflows past it (landing Y=-2.5) and should wrap onto the South wall.
        var lightDirection = new Vector3(0, -0.6, -0.8);
        Vector3[] corners =
        [
            new Vector3(-0.5, -1.0, 2.0), new Vector3(0.5, -1.0, 2.0),
            new Vector3(0.5, -0.3, 2.0), new Vector3(-0.5, -0.3, 2.0),
        ];
        Vector3[] projection =
        [
            new Vector3(-0.5, -2.5, 0), new Vector3(0.5, -2.5, 0),
            new Vector3(0.5, -1.8, 0), new Vector3(-0.5, -1.8, 0),
        ];

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.Floor, WallOrientation.West, corners, projection, lightDirection);

        Assert.Equal(2, patches.Count);

        var floorPatch = Assert.Single(patches, p => p.Surface == RoomSurface.Floor);
        Assert.All(floorPatch.Polygon, v => Assert.Equal(0.0, v.Z, precision: 9));
        Assert.All(floorPatch.Polygon, v => Assert.True(v.Y >= -2.0 - 1e-9 && v.Y <= -1.8 + 1e-9));
        Assert.Contains(floorPatch.Polygon, v => Math.Abs(v.Y - -2.0) < 1e-6);

        var southPatch = Assert.Single(patches, p => p.Surface == RoomSurface.SouthWall);
        Assert.All(southPatch.Polygon, v => Assert.Equal(-2.0, v.Y, precision: 6));
        Assert.Contains(southPatch.Polygon, v => Math.Abs(v.Z - 2.0 / 3.0) < 1e-4);
    }

    [Fact]
    public void Clip_OverflowPastAWallEdge_WrapsOntoTheAdjacentWallAcrossTheCorner()
    {
        // Same idea, but overflowing sideways off the North wall (past its +X edge,
        // halfWidth=3) onto the East wall instead - confirms the algorithm isn't floor-specific.
        var lightDirection = new Vector3(0.7071068, 0.7071068, 0);
        Vector3[] corners =
        [
            new Vector3(0.5, 1.5, 1.0), new Vector3(0.5, -1.0, 1.0),
            new Vector3(0.5, -1.0, 1.5), new Vector3(0.5, 1.5, 1.5),
        ];
        Vector3[] projection =
        [
            new Vector3(1.0, 2, 1.0), new Vector3(3.5, 2, 1.0),
            new Vector3(3.5, 2, 1.5), new Vector3(1.0, 2, 1.5),
        ];

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.NorthWall, WallOrientation.South, corners, projection, lightDirection);

        Assert.Equal(2, patches.Count);

        var northPatch = Assert.Single(patches, p => p.Surface == RoomSurface.NorthWall);
        Assert.All(northPatch.Polygon, v => Assert.Equal(2.0, v.Y, precision: 6));
        Assert.All(northPatch.Polygon, v => Assert.True(v.X <= 3.0 + 1e-6));

        var eastPatch = Assert.Single(patches, p => p.Surface == RoomSurface.EastWall);
        Assert.All(eastPatch.Polygon, v => Assert.Equal(3.0, v.X, precision: 6));
        Assert.Contains(eastPatch.Polygon, v => Math.Abs(v.Y - 1.5) < 1e-3);
    }

    [Fact]
    public void Clip_OverflowAboveWallHeight_IsDroppedSinceNoCeilingIsModeled()
    {
        Vector3[] projection =
        [
            new Vector3(0, -2, 1.0), new Vector3(1, -2, 1.0),
            new Vector3(1, -2, 4.0), new Vector3(0, -2, 4.0),
        ];
        var corners = projection; // unused: nothing here ever gets re-projected (no ceiling to wrap onto)

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.SouthWall, WallOrientation.North, corners, projection,
            lightDirection: new Vector3(0, 0.7071068, -0.7071068));

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.SouthWall, patch.Surface);
        Assert.All(patch.Polygon, v => Assert.True(v.Z <= 3.0 + 1e-9));
        Assert.Contains(patch.Polygon, v => Math.Abs(v.Z - 3.0) < 1e-6);
    }

    [Fact]
    public void Clip_TouchingButNotCrossingABoundary_DoesNotCreateAnAdjacentPatch()
    {
        Vector3[] projection =
        [
            new Vector3(-0.5, 1.0, 0), new Vector3(0.5, 1.0, 0),
            new Vector3(0.5, 2.0, 0), new Vector3(-0.5, 2.0, 0), // top edge sits exactly on the floor's Y bound
        ];
        var corners = projection; // unused: the polygon never actually crosses the boundary

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.Floor, WallOrientation.West, corners, projection, lightDirection: new Vector3(0, 0, -1));

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.Floor, patch.Surface);
        Assert.Equal(projection, patch.Polygon);
    }

    [Fact]
    public void Clip_WhenOverflowCannotReachItsNeighborsPlane_DropsItWithoutThrowing()
    {
        // Light travelling due south (0,-1,0) has no sideways drift at all, so a patch that
        // spills past the South wall's +X edge can never continue on to reach the East
        // wall's plane by travelling further along the same direction - that overflow
        // should simply be dropped, not throw.
        var lightDirection = new Vector3(0, -1, 0);
        Vector3[] corners =
        [
            new Vector3(2.0, -1.0, 1.0), new Vector3(4.0, -1.0, 1.0),
            new Vector3(4.0, -1.0, 1.5), new Vector3(2.0, -1.0, 1.5),
        ];
        Vector3[] projection =
        [
            new Vector3(2.0, -2, 1.0), new Vector3(4.0, -2, 1.0),
            new Vector3(4.0, -2, 1.5), new Vector3(2.0, -2, 1.5),
        ];

        var patches = IlluminationPatchClipper.Clip(
            TestRoom, RoomSurface.SouthWall, WallOrientation.North, corners, projection, lightDirection);

        var patch = Assert.Single(patches);
        Assert.Equal(RoomSurface.SouthWall, patch.Surface);
        Assert.DoesNotContain(patches, p => p.Surface == RoomSurface.EastWall);
    }
}
```

- [ ] **Step 5: Run tests to verify they fail**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: build error - `IlluminationPatchClipper` does not exist in the current context.

- [ ] **Step 6: Create `IlluminationPatchClipper`**

Create `src/Chiaroscuro.Core/Geometry/IlluminationPatchClipper.cs`:

```csharp
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
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: all tests pass, including the 6 new `IlluminationPatchClipperTests` and every pre-existing test in that project.

- [ ] **Step 8: Verify the whole solution still builds**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors (this task hasn't touched `IlluminationResult`'s shape, so `Chiaroscuro.UI`/`Chiaroscuro.UI.Tests` are unaffected).

- [ ] **Step 9: Commit**

```bash
git add src/Chiaroscuro.Core/Geometry/Room.cs src/Chiaroscuro.Core/Geometry/RayTracer.cs \
        src/Chiaroscuro.Core/Geometry/IlluminationPatchClipper.cs \
        tests/Chiaroscuro.Core.Tests/Geometry/IlluminationPatchClipperTests.cs
git commit -m "Add IlluminationPatchClipper for physically-correct light patch boundary clipping"
```

---

### Task 2: Wire `Patches` into `RayTracer.Trace` and `SceneBuilder`

**Files:**
- Modify: `src/Chiaroscuro.Core/Geometry/RayTracer.cs` (`IlluminationResult` gains `Patches`; `Trace` calls the clipper)
- Modify: `tests/Chiaroscuro.Core.Tests/Geometry/RayTracerTests.cs` (add one integration test)
- Modify: `src/Chiaroscuro.UI/Viewport/SceneBuilder.cs` (`Build` draws one fill per patch)
- Modify: `tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs` (fix 2 call sites, add one test)

**Interfaces:**
- Consumes: `IlluminationPatchClipper.Clip(...)` and `LandingPatch` (both from Task 1).
- Produces: `IlluminationResult(RoomSurface Surface, Vector3 CenterPoint, Vector3[] IlluminatedPolygon, IReadOnlyList<LandingPatch> Patches)` - a 4th required positional field, so every existing `new IlluminationResult(...)` call site needs updating (only `SceneBuilderTests.cs` constructs one directly; `RayTracer.Trace` is the only other place, updated in this same task).

This task is why Task 1 was split out separately: this one atomic commit is the only point where the breaking constructor change happens, so the tree is never left with a compile error in between.

- [ ] **Step 1: Add `Patches` to `IlluminationResult` and wire `Trace`**

In `src/Chiaroscuro.Core/Geometry/RayTracer.cs`, replace the `IlluminationResult` record:

```csharp
public readonly record struct IlluminationResult(RoomSurface Surface, Vector3 CenterPoint, Vector3[] IlluminatedPolygon);
```

with:

```csharp
public readonly record struct IlluminationResult(
    RoomSurface Surface,
    Vector3 CenterPoint,
    Vector3[] IlluminatedPolygon,
    IReadOnlyList<LandingPatch> Patches);
```

and update its doc comment (the `<param>` block) to:

```csharp
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
```

Then, in the same file, replace the `Trace` method:

```csharp
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

        // Project every window corner along the same light direction onto the same
        // plane the center point landed on - together these four points form the
        // illuminated polygon spec §3.2 calls for.
        var illuminatedPolygon = window.GetCorners(room)
            .Select(corner => ProjectOntoPlane(corner, lightDirection, targetPlane))
            .ToArray();

        return new IlluminationResult(nearestHit.Surface, nearestHit.Point, illuminatedPolygon);
    }
```

with:

```csharp
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
```

- [ ] **Step 2: Add the integration test**

In `tests/Chiaroscuro.Core.Tests/Geometry/RayTracerTests.cs`, add this test inside the `RayTracerTests` class (after the existing `Trace_IlluminatedPolygon_HasFourCornersOnTheTargetSurfacePlane` test):

```csharp
    [Fact]
    public void Trace_WhenLightOverflowsPastThePrimarySurface_PopulatesMultiplePatches()
    {
        // A narrow room and a wide, off-center East window pushes part of the light patch
        // past the West wall's own Y bound - RayTracer.Trace should wrap that overflow onto
        // the North wall rather than leaving it in the raw, unclipped IlluminatedPolygon.
        var room = new Room(Width: 4, Length: 2, Height: 3);
        var window = new Window(WallOrientation.East, HorizontalOffset: 0, SillHeight: 1, Width: 1.5, Height: 1);
        var sunPosition = new SolarPosition(ElevationDegrees: 5, AzimuthDegrees: 95);

        var result = RayTracer.Trace(room, window, sunPosition);

        Assert.NotNull(result);
        Assert.Equal(RoomSurface.WestWall, result.Value.Surface);
        Assert.Equal(2, result.Value.Patches.Count);

        var westPatch = Assert.Single(result.Value.Patches, p => p.Surface == RoomSurface.WestWall);
        Assert.All(westPatch.Polygon, v => Assert.True(v.Y <= 1.0 + 1e-6));

        var northPatch = Assert.Single(result.Value.Patches, p => p.Surface == RoomSurface.NorthWall);
        Assert.All(northPatch.Polygon, v => Assert.Equal(1.0, v.Y, precision: 6));
    }
```

- [ ] **Step 3: Run Core tests, verify all pass**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: all tests pass, including the 4 pre-existing `RayTracerTests` (unchanged - they never read `Patches`) and the new one.

- [ ] **Step 4: Update `SceneBuilder.Build` to draw every patch**

In `src/Chiaroscuro.UI/Viewport/SceneBuilder.cs`, replace:

```csharp
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
```

with:

```csharp
    public static IReadOnlyList<ScenePrimitive> Build(Room room, Window window, IlluminationResult? illumination)
    {
        var primitives = new List<ScenePrimitive>();

        AddRoomWireframe(primitives, room);
        AddRectangleEdges(primitives, window.GetCorners(room), WireframeColor);

        if (illumination is { } hit)
        {
            // The cone keeps using the raw, unclipped projection - splitting it across
            // surfaces too is out of scope for this fix (see the design doc). Only the
            // filled landing patch respects the physically-clipped, possibly-multi-surface
            // shape.
            AddLightCone(primitives, window.GetCorners(room), hit.IlluminatedPolygon);

            foreach (var patch in hit.Patches)
            {
                primitives.Add(new ScenePolygon(patch.Polygon, LandingPatchColor));
            }
        }

        return primitives;
    }
```

- [ ] **Step 5: Fix the existing `SceneBuilderTests` call sites and add a multi-patch test**

In `tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs`, replace the whole file:

```csharp
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
}
```

- [ ] **Step 6: Run the full solution's tests, verify everything is green**

Run: `dotnet test`
Expected: `Passed!` for both `Chiaroscuro.Core.Tests` and `Chiaroscuro.UI.Tests`, 0 failures across the whole solution.

- [ ] **Step 7: Commit**

```bash
git add src/Chiaroscuro.Core/Geometry/RayTracer.cs tests/Chiaroscuro.Core.Tests/Geometry/RayTracerTests.cs \
        src/Chiaroscuro.UI/Viewport/SceneBuilder.cs tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs
git commit -m "Draw physically-clipped light patches instead of the raw unclipped polygon"
```
