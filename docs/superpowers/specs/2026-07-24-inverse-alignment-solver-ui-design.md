# Phase 4: Inverse Alignment Solver UI — Design

## Context

`SPEC.md` §5 Phase 4 calls for exposing the inverse alignment solver (find
dates across the year when the sun lines up with a target point) through the
UI, with an "interactive timeline view displaying top matching solar
alignment dates." Phases 1-3 are complete: `Chiaroscuro.Core` already has a
fully working, unit-tested solver
(`InverseSolver.InverseAlignmentSolver.FindAlignments`) that sweeps 365 days
in 15-minute steps and returns every `AlignmentMatch` within an angular
tolerance - it was built ahead of schedule during Phase 1. What's missing is
entirely the UI/integration layer: letting the user specify a target point
and tolerance, running the sweep reactively like every other input in this
app, reducing the (potentially large) raw match list down to a clean set of
"Golden Highlight Cards," and - added during this design review - a visual
indicator of the target point and its tolerance radius in the 3D viewport.

## Decisions

- **Target point: numeric X/Y/Z fields**, not click-to-pick in the viewport
  (a much larger feature on its own - `RoomViewport` has no hit-testing
  today) and not an always-implicit "wherever light lands now" (too
  inflexible, and undefined when nothing is currently illuminated). Seeded
  once at construction from the initial `Illumination.CenterPoint` (or the
  floor center `(0,0,0)` if nothing is illuminated yet), then purely
  user-controlled - it must never be silently overwritten by a later
  `Illumination` recalculation while the user is mid-edit.
- **Reactive, not button-triggered.** Wired into the same
  `OnXChanged → Recalculate()` pipeline as everything else. ~35,000 iterations
  of cheap trig math per run is not a responsiveness concern.
- **Tolerance is a user-adjustable field** (`Tolerance (°)`, default `2°`),
  not a hidden constant - it directly controls how many/which matches
  appear, and the spec calls it out explicitly as a parameter.
- **Results are collapsed to one card per day, top N by closeness, displayed
  chronologically.** Raw solver output can have many 15-minute-step matches
  clustered around the same event; showing every one would flood the "Golden
  Highlight Cards" strip regardless of the app's own theme. The app decides
  *which* days are worth showing by closeness, but presents them as an
  actual timeline (chronological), matching the spec's own "top matching...
  dates" phrasing.
- **Clicking a card jumps `Date`/`TimeOfDay` to that match**, so the payoff
  of finding an alignment is immediately seeing it rendered, not just
  reading text.
- **3D viewport gets a target marker and a tolerance ring**, both outlines
  (not filled), in the existing wireframe violet (`ChiaroscuroForegroundColor`,
  `#9491C0`) rather than any amber/gold/yellow shade - those colors are
  reserved for real light per `SPEC.md` §1, and these are measurement
  overlays, not light.

## API shape

### `Chiaroscuro.Core.InverseSolver.AlignmentMatchSummarizer` (new)

```csharp
public static class AlignmentMatchSummarizer
{
    public static IReadOnlyList<AlignmentMatch> SummarizeTopMatches(IReadOnlyList<AlignmentMatch> matches, int maxResults);
}
```

Groups by calendar day, keeps each day's closest (`AngleDifferenceDegrees`
ascending) match, takes the top `maxResults` days by closeness, returns them
sorted chronologically. Pure NodaTime/domain logic, no UI dependency -
unit-testable the same way `InverseAlignmentSolver` already is.

### `Chiaroscuro.Core.Geometry.Vector3.Cross` (new)

```csharp
public Vector3 Cross(Vector3 other);
```

Standard 3D cross product, added alongside the existing `Dot`/`Normalized`/
`AngleTo`. Needed to build an orthonormal basis perpendicular to the
window→target direction for the tolerance ring (see below).

### `Chiaroscuro.UI.ViewModels.MainViewModel` (extended)

New `[ObservableProperty]` fields:
- `TargetX`, `TargetY`, `TargetZ` (`decimal?`) - raw numeric inputs, same
  pattern as `RoomWidth` etc.
- `ToleranceDegrees` (`decimal?`, default `2m`).
- `TargetPoint` (`Vector3?`) - computed from `TargetX/Y/Z` in `Recalculate()`
  the same way `Room`/`Window` are computed from their raw fields; null if
  any of the three is empty. `RoomViewport` binds to this directly.
- `AlignmentMatches` (`IReadOnlyList<AlignmentMatchCard>`) - the summarized,
  UI-formatted results.

New UI-only record (lives with the ViewModels, no Core dependency):

```csharp
public sealed record AlignmentMatchCard(string DateLabel, string TimeLabel, string AngleLabel, LocalDate Date, LocalTime Time);
```

`DateLabel`/`TimeLabel`/`AngleLabel` are pre-formatted display strings
(matching how `ResultText` is already a pre-formatted string rather than
something bound through XAML converters); `Date`/`Time` are kept
un-formatted so the click-to-jump command can set `Date`/`TimeOfDay`
directly without re-parsing display text.

### `Chiaroscuro.UI.Viewport.SceneBuilder` (extended)

`Build` gains two more parameters: `Vector3? target, double? toleranceDegrees`.
When `target` is set, a new private `AddTargetIndicator` helper adds:
- A small crosshair (two short `SceneLine` segments) exactly at `target`.
- If `toleranceDegrees` is also set: a ring of `SceneLine` segments (a
  closed N-gon approximating a circle, `N = 32`) of radius
  `distance(windowCenter, target) × tan(toleranceRadians)` (tolerance
  converted from degrees to radians before calling `Math.Tan`), centered on
  `target`, lying in the plane perpendicular to the window→target direction
  (built via `Vector3.Cross` to get an orthonormal in-plane basis) - i.e. a
  reticle facing the window, not flattened onto whichever room surface
  happens to be nearby (the target isn't guaranteed to sit exactly on one,
  and perpendicular-to-the-ray is also the mathematically exact slice of the
  angular tolerance cone - anywhere else, it would generally be an ellipse,
  not a circle).

Both use the existing `WireframeColor` (`#9491C0`) - no new `SceneColor`
constant needed.

### `Chiaroscuro.UI.Views.RoomViewport` (extended)

Two new `StyledProperty`s, `TargetPoint` (`Vector3?`) and `ToleranceDegrees`
(`double?`), bound from `MainWindow.axaml` to the view model and threaded
into `SceneBuilder.Build`.

## Data flow

`Recalculate()` gains, after its existing `Room`/`Window`/`Illumination`
computation:
1. Bridge `UtcOffsetHours` into a `DateTimeZone` via
   `DateTimeZone.ForOffset(Offset.FromTimeSpan(...))` - same offset math
   already used elsewhere in the method.
2. Build `TargetPoint` from `TargetX/Y/Z`.
3. If `TargetPoint` and `ToleranceDegrees` are both present (alongside the
   already-required core fields), call
   `InverseAlignmentSolver.FindAlignments(Room, Window, TargetPoint.Value,
   Latitude, Longitude, zone, startDate, ToleranceDegrees)` - reusing the
   existing `Date` field as the sweep's start date, since a year-long sweep
   covers the same annual pattern regardless of exactly where in the year it
   starts, avoiding a second date picker - then
   `AlignmentMatchSummarizer.SummarizeTopMatches(raw, 15)`, then map to
   `AlignmentMatchCard`s.
4. If `TargetPoint`/`ToleranceDegrees` are missing but the core fields are
   present, `Room`/`Window`/`Illumination`/`ResultText` still update as
   before; `AlignmentMatches` is left at its last-known-good value,
   consistent with how the rest of `Recalculate()` already treats missing
   optional input.

`MainWindow.axaml`: left sidebar gets one more card, "Target Point" (X/Y/Z
+ Tolerance fields, same style as the existing cards). The bottom results
`Border` gets a second piece below the existing `ResultText`: a horizontally
scrollable `ItemsControl` of small amber-accented cards bound to
`AlignmentMatches`, each with a `[RelayCommand]` that sets `Date`/`TimeOfDay`
from the card's `Date`/`Time` when clicked.

## Testing

- **`AlignmentMatchSummarizerTests`** (new, Core): given a hand-built list
  spanning multiple days with several matches each, verify only the
  closest-per-day survives, the result is capped at `maxResults`, and it's
  sorted chronologically (not by closeness).
- **`Vector3Tests`**: add cross-product cases (e.g. `(1,0,0) × (0,1,0) =
  (0,0,1)`, anti-commutativity, cross with a parallel vector = zero).
- **`SceneBuilderTests`**: with `target` set, the crosshair primitives are
  present; with `target` and `toleranceDegrees` both set, the ring's
  vertices are all equidistant (within tolerance) from `target` at the
  expected radius; with `target` unset, no indicator primitives appear.
- View-model-level behavior (reactive wiring, card formatting, the
  click-to-jump command) is not unit tested, consistent with the rest of
  `MainViewModel` - verified manually, same as Phases 2-3.

## Error handling

No new failure modes. `InverseAlignmentSolver.FindAlignments` already has no
failure path other than "the loop finds zero matches," which is normal,
expected output (an empty `AlignmentMatches` list, shown as no cards).
Missing/blank numeric inputs are handled the same way the rest of
`Recalculate()` already handles them - skip the affected computation, keep
last-known-good values, no exceptions.

## Out of scope for this pass

- Click-to-pick the target point in the 3D viewport (numeric fields only,
  per the decision above).
- Any validation that the target point is physically inside the room or on
  a real surface - an out-of-room target simply renders its crosshair
  outside the wireframe, which is its own useful feedback.
- Elliptical (rather than circular) tolerance rendering for targets on
  oblique surfaces - the perpendicular-to-ray circle is treated as good
  enough, not a physically-exact projection onto a surface.
