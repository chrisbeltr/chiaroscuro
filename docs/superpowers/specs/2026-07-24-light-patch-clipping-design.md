# Light Patch Boundary Clipping — Design

## Context

`Chiaroscuro.Core.Geometry.RayTracer.Trace` computes where sunlight passing
through a `Window` lands inside a `Room`: it finds the nearest surface the
window's *center* ray hits, then projects all four window corners onto that
same surface's plane to build `IlluminationResult.IlluminatedPolygon` - the
light patch drawn by `Chiaroscuro.UI`'s `SceneBuilder`/`RoomViewport`.

That projection only checks that the *center* point lands within the target
surface's physical bounds (`Room.IsWithinSurfaceBounds`). The other three
corners are never bounds-checked, so near a room edge or corner, part of the
projected quad can fall outside the surface it's nominally drawn on - visibly
poking through the room's wireframe wall/floor boundary in the viewport.

This is purely a `Chiaroscuro.Core` geometry gap; `Chiaroscuro.UI` (Phase 3,
already shipped) just draws whatever polygon `RayTracer` hands it.

## Decisions

- **Physically correct, not a rendering trick.** The fix continues tracing
  the same light ray past the primary surface's edge to find where it
  actually lands on the neighboring surface, rather than clipping-and-folding
  or clipping-and-discarding in the UI layer. `Chiaroscuro.Core`'s output
  should reflect real geometry, since it's the shared source of truth (also
  used by the inverse solver).
- **`IlluminatedPolygon` stays exactly as-is.** It remains the raw, unclipped,
  always-4-corner (in `Window.GetCorners` order) projection onto the primary
  surface. `SceneBuilder`'s light-cone side faces keep consuming it
  unchanged - splitting the cone across surfaces too is out of scope for this
  pass (see below).
- **A new `Patches` list carries the physically-clipped result.** Each entry
  is a `LandingPatch(RoomSurface Surface, Vector3[] Polygon)`; the polygons
  are what `SceneBuilder` now fills instead of the single unclipped quad.
  Keeping both fields is deliberate, not duplication: they have different
  vertex-count/ordering guarantees (see Q&A during design review) and
  different consumers (cone vs. fill).
- **Fully general wrap depth.** The clip-and-continue recursion isn't capped
  at one hop; it keeps going until every part of the light patch is either
  accounted for on some surface or has nowhere left to go (the unmodeled
  ceiling). A visited-surface set bounds recursion to at most 5 levels.

## API shape

`Chiaroscuro.Core/Geometry/RayTracer.cs`:

```csharp
public readonly record struct LandingPatch(RoomSurface Surface, Vector3[] Polygon);

public readonly record struct IlluminationResult(
    RoomSurface Surface,
    Vector3 CenterPoint,
    Vector3[] IlluminatedPolygon,
    IReadOnlyList<LandingPatch> Patches);
```

`Surface`/`CenterPoint`/`IlluminatedPolygon` keep their current meaning and
computation, untouched. `Patches` is new: one or more entries whose polygons
together cover the same physical light shadow, each strictly within its own
surface's real rectangular extent. In the common case (no overflow) it's a
single entry shape-equal to `IlluminatedPolygon`.

## Algorithm

New file `Chiaroscuro.Core/Geometry/IlluminationPatchClipper.cs`, called from
`RayTracer.Trace` once the existing center-ray/primary-surface logic has run.

Each `RoomSurface` has up to 4 physical edges, and each edge borders exactly
one neighboring surface, or none:

- **Floor**: all 4 edges border a wall (North/South/East/West).
- **A wall**: its two side edges border the two adjacent walls, its bottom
  edge borders the floor, its top edge borders nothing (no ceiling surface is
  modeled anywhere in `Chiaroscuro.Core` - light spilling above wall height
  is simply lost, matching the existing "no valid hit" semantics elsewhere).

Given the window's 4 corners already projected onto the primary surface's
plane (i.e. `IlluminatedPolygon`, tagged internally with which original
window corner each vertex came from):

1. Clip the polygon against the primary surface's 4 edges via
   Sutherland-Hodgman (cheap - these are axis-aligned half-planes, and the
   polygon stays convex throughout since we're only ever intersecting convex
   shapes with half-planes).
2. Whatever remains inside all 4 edges becomes a `LandingPatch` for that
   surface.
3. Whatever was clipped away at a given edge: if that edge has an unvisited
   neighboring surface, re-derive each cut-off vertex's position by
   re-projecting its *original window corner* (not the already-clipped
   point) along the same light direction onto the neighbor's plane, then
   recurse the whole clip step onto that neighbor.
4. Vertices sitting exactly on a shared edge (inserted by the clip itself)
   don't need re-projecting - they already satisfy both surfaces' plane
   equations, so they pass through unchanged.
5. A `HashSet<RoomSurface>` of visited surfaces (seeded with the window's own
   wall, which was never a candidate anyway) guarantees termination: each
   recursive call adds exactly one surface, and there are only 5 to exhaust.

## Data flow

No change outside `Chiaroscuro.Core` and `Chiaroscuro.UI.Viewport.SceneBuilder`:

- `RayTracer.Trace` calls `IlluminationPatchClipper` after computing today's
  `Surface`/`CenterPoint`/`IlluminatedPolygon`, and includes the result as
  `Patches`.
- `SceneBuilder.Build` keeps calling `AddLightCone` with `IlluminatedPolygon`
  unchanged, and replaces its single
  `primitives.Add(new ScenePolygon(hit.IlluminatedPolygon, LandingPatchColor))`
  with one `ScenePolygon` per entry in `hit.Patches`.
- `MainViewModel` needs no changes - it already just stores whatever
  `IlluminationResult?` `RayTracer.Trace` returns.

## Testing

- **`IlluminationPatchClipperTests`** (new): no-overflow case (single patch,
  matches unclipped shape); floor-to-wall single wrap; wall-to-wall wrap
  across a room corner (confirms the algorithm isn't floor-special-cased);
  spill-above-wall-height is silently dropped (no ceiling surface, no
  crash/empty patch); a grazing/tangent case doesn't produce a degenerate
  zero-area patch.
- **`RayTracerTests`**: existing `Surface`/`CenterPoint`/`IlluminatedPolygon`
  tests unchanged; add one integration-style test asserting `Trace` populates
  `Patches` correctly for a known overflow scenario.
- **`SceneBuilderTests`**: update existing `IlluminationResult` construction
  call sites to include `Patches`; add a test that 2 patches yield 2
  `ScenePolygon` fill primitives while the cone still yields exactly 4 quads
  regardless.

## Error handling

No new failure modes. `RayTracer.Trace` still returns `null` under exactly
the same conditions as today (sun below horizon, center ray doesn't land in
the room). Everything in this design only changes the *shape* of `Patches`
within an already-non-null result.

## Out of scope for this pass

- Splitting the light-cone side faces across multiple surfaces - they keep
  using the simple unclipped `IlluminatedPolygon`, per explicit design
  decision (the visual complaint being fixed is the filled patch, not the
  cone).
- Modeling a ceiling surface - light spilling above wall height is dropped,
  consistent with how `Chiaroscuro.Core` already has no `RoomSurface.Ceiling`
  case anywhere.
