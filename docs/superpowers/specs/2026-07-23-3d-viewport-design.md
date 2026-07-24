# Phase 3: Hardware-Accelerated 3D Render Viewport — Design

## Context

`SPEC.md` §5 Phase 3 calls for a 3D viewport rendering the room as a wireframe,
with a translucent golden/amber light cone and bright yellow floor/wall
projection patch showing where sunlight lands, plus interactive orbit/pan/zoom.
Phase 1 (`Chiaroscuro.Core`) and Phase 2 (`Chiaroscuro.UI` shell + reactive
parameter panel) are complete and pushed to `main`.

Decisions made during spec review (this document) and earlier planning:
- **3D render engine: SkiaSharp.** Avalonia already renders through Skia on
  every platform, including WASM, so a custom Skia draw operation (manual 3D→2D
  projection, no real GPU pipeline) needs no extra native/WebGL interop. This
  was the deferred decision noted in `divine-claude-plan.md`.
- **Layout**: the 3D viewport replaces the current right-side result
  `TextBlock` in `MainWindow.axaml`. The sun altitude/azimuth/illuminated-surface
  text moves to a thin results strip along the bottom of the window, matching
  `SPEC.md` §4's mockup (viewport as the main panel, a separate results strip
  below it).
- **Camera controls**: full interactivity in this pass (not deferred to a
  follow-up). Left-drag orbits, mouse wheel zooms. **No panning** — the
  camera's look-at target is always the room's center (recomputed if room
  dimensions change), it can never be moved off-center.

## Architecture

Four new pieces, all in `Chiaroscuro.UI` (not `Chiaroscuro.Core`, which stays
free of rendering concerns per its existing design). The first three are plain
C# with no SkiaSharp/Avalonia dependency, so they're unit-testable the same
way Core's math is:

### `OrbitCamera`
Plain class holding camera state: `Yaw`, `Pitch`, `Distance` (all `double`),
plus a `Target` (`Chiaroscuro.Core.Geometry.Vector3`, always set to the room's
center — `(0, 0, Height / 2)` given the room's floor-centered origin
convention). Exposes:
- `Orbit(double deltaYaw, double deltaPitch)` — mutates `Yaw`/`Pitch`. Pitch is
  clamped to roughly ±89° so the camera can never flip past vertical.
- `Zoom(double deltaDistance)` — mutates `Distance`, clamped to a sane
  min/max (close enough to inspect detail, far enough to never clip through a
  wall or invert).
- `GetViewMatrix()` / `GetProjectionMatrix(double aspectRatio)` — built from
  `System.Numerics.Matrix4x4.CreateLookAt` /
  `Matrix4x4.CreatePerspectiveFieldOfView`. `System.Numerics` is already in
  the BCL (no new NuGet package) and is WASM-safe for Phase 5.

### `ViewportProjector`
A pure function/static class: given a `Chiaroscuro.Core.Geometry.Vector3`
world-space point, an `OrbitCamera`, and the viewport's pixel `Size`, returns
a 2D screen point plus the point's camera-space depth (for draw ordering).
Converts the Core `Vector3` into `System.Numerics.Vector3` for the matrix
transform, then maps clip space to pixel coordinates.

### `SceneBuilder`
Takes `Room`, `Window`, `SolarPosition`, and `IlluminationResult?` and
produces a flat list of drawable primitives in room-space:
- Room wireframe edges (12 edges of the box) and window frame edges (4 edges
  of the aperture rectangle) — always present.
- If `IlluminationResult` is non-null: four translucent quad faces connecting
  each window corner to its corresponding projected corner on the illuminated
  surface (the "light cone" — really a frustum, since the window opening and
  the landing patch are both rectangles), plus the illuminated polygon itself
  as a filled patch.
- If `IlluminationResult` is null (sun below horizon, or the ray simply
  doesn't land inside the room): only the wireframe primitives are emitted.
  This is normal, expected output, not an error — `RayTracer.Trace` already
  models "no hit" as `null`, and `SceneBuilder` just passes that through
  without any new failure mode.

Primitives are simple discriminated shapes: a line segment (two `Vector3`
endpoints, a color) or a filled polygon (an array of `Vector3` corners, a
fill color with alpha). Colors reuse the existing `Chiaroscuro*Brush`/color
resources: wireframe and window frame in the app's cool violet/slate palette,
light-cone faces and the landing patch in amber/gold/sun-yellow with
transparency — never mixed, staying consistent with `SPEC.md`'s "amber only
ever means light" rule.

### `RoomViewport`
The actual Avalonia `Control`. Deliberately thin:
- Owns one `OrbitCamera` instance.
- On `Render`, calls `SceneBuilder` with its currently-bound `Room`/`Window`/
  `SolarPosition`/`IlluminationResult?`, projects every primitive through
  `ViewportProjector`, sorts filled polygons back-to-front by camera-space
  depth (painter's algorithm — no z-buffer needed for a scene this simple),
  and paints via a custom Skia draw operation (`ICustomDrawOperation` leasing
  the Skia canvas).
- Handles `PointerPressed`/`PointerMoved`/`PointerReleased` to drive
  `OrbitCamera.Orbit(...)` on left-drag, and `PointerWheelChanged` to drive
  `OrbitCamera.Zoom(...)`. Calls `InvalidateVisual()` after any camera or
  scene-data change.

## Data flow

`MainViewModel.Recalculate()` already computes `Room`, `Window`,
`SolarPosition`, and the `IlluminationResult?` locally on every parameter
change (via the existing `OnXChanged` → `Recalculate()` reactive pipeline from
Phase 2). These get promoted from locals to `[ObservableProperty]` fields so
`RoomViewport` can bind to them directly from `MainWindow.axaml` and react to
`PropertyChanged` by re-invalidating — no second reactive pipeline, just
extending the one that already exists.

`MainWindow.axaml`'s right-side panel changes from a single `TextBlock` to a
`Grid` with the `RoomViewport` filling the main area and a thin bottom `Border`
(reusing the existing `Border.panel` style) showing the same sun
elevation/azimuth/surface text that's there today.

## Testing

New `Chiaroscuro.UI.Tests` xUnit project, mirroring the existing
`Chiaroscuro.Core.Tests` pattern:
- **`OrbitCamera`**: a point at the look-at target projects to the viewport's
  center regardless of yaw/pitch/distance; orbiting 180° swaps which room
  wall faces the camera; pitch/distance clamping actually clamps at the
  boundaries.
- **`ViewportProjector`**: known point + known camera state → known pixel
  coordinate (a few hand-checked cases, similar in spirit to Phase 1's
  known-reference-value tests).
- **`SceneBuilder`**: given a room+window with `IlluminationResult == null`,
  emits only wireframe primitives; given a non-null result, also emits the
  four light-cone faces and the landing patch, with corners matching the
  `IlluminationResult`'s data.

`RoomViewport` itself (the literal Skia paint call) is not unit tested,
consistent with the rest of `Chiaroscuro.UI` — verified manually by running
the app, same as Phase 2.

## Error handling

No new failure modes. `IlluminationResult?` is already nullable and
`RayTracer.Trace` already models "no valid hit" that way; `SceneBuilder` just
consumes that existing contract. Camera state (yaw/pitch/distance) is always
kept in-bounds by `OrbitCamera`'s own clamping, so there's no invalid camera
state to guard against elsewhere.

## Out of scope for this pass

- Panning (explicitly rejected — orbit target is permanently fixed to room
  center).
- Any compass/axis orientation indicator, ground reference grid, or
  camera-reset control — not requested; can be added later if wanted.
- WASM-specific rendering concerns — deferred to Phase 5 per the existing
  roadmap; `System.Numerics` and Skia were chosen partly because they don't
  block that later phase, but no WASM-specific work happens now.
