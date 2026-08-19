# Migration parity checklist

Run at the Phase 4 and Phase 6 gates (see the migration plan). Confirms the ASP.NET
API + React frontend produce the same results as the Avalonia app for a fixed set of
scenarios. Since `Chiaroscuro.Core` is untouched by the migration and shared by both
stacks, any difference here should only ever be HTTP/DTO-layer rounding, never
algorithmic drift.

**Method:** `Chiaroscuro.Core.Tests` (61 tests, unmodified) is the parity oracle for the
calculation engine itself. This checklist additionally drives the same/equivalent
scenarios through the live `Chiaroscuro.Api` HTTP endpoints to confirm the DTO mapping
layer carries those results through losslessly - the same property `Chiaroscuro.Api.Tests`
(8 tests) asserts against fixture inputs. Results below were captured via `curl` against
`dotnet run --project src/Chiaroscuro.Api` on 2026-08-19.

## Results

### 1. NYC default baseline
Room 6x5x3, South window (offset 0, sill 1, 1.2x1.5), lat/long 40.7128/-74.006 (the
app's own hardcoded default, suncalc.org-verified per `MainViewModel.cs`), 2026-08-19
13:15 local (UTC-4).

```
sunPosition: elevation 61.68°, azimuth 187.93°
illumination: Floor, center (0.130, -1.566, 0)
```
Sane for early-afternoon midsummer at this latitude (near-overhead, azimuth just past
due south). ✅

### 2. High-latitude near-polar-day
Tromsø, Norway (69.6492, 18.9553), summer solstice noon local (2026-06-21 12:00, UTC+2).

```
sunPosition: elevation 43.28°, azimuth 165.45°
illumination: Floor, center (-0.467, -0.701, ~0)
```
Sun stays well above the horizon at local noon in June this far north (consistent with
near-continuous daylight), without pinning at 90° - correct, since Tromsø is not exactly
at the pole. ✅

### 3. Room rotation ≠ 0
Same as #1, `roomRotationDegrees: 45`.

```
sunPosition: elevation 61.68°, azimuth 187.93° (unchanged - correct, sun position is
  independent of building orientation)
illumination: Floor, center (-0.568, -1.748, 0)
```
Center point differs from #1 by more than a simple 45° rotation of the original point -
expected, since `RotationDegrees` rotates the *building* while the sun's real-world
direction stays fixed, so the window's angle of incidence genuinely changes (not just a
rigid transform of the old answer). ✅

### 4. Sun below horizon
Same as #1, but 2026-08-19 03:00 local (UTC-4).

```
sunPosition: elevation -29.74°, azimuth 34.25°
illumination: null
```
No surface illuminated while the sun is below the horizon, matching
`RayTracerTests.Trace_ReturnsNull_WhenLightTravelsAwayFromTheRoomInterior`. ✅

### 5. Inverse-solver round-trip (exact fixture from `InverseAlignmentSolverTests`)
Room 6x5x3, South window (offset 0, sill 1, 1.2x1.5), lat/long 40.7128/-74.006, sweep
start 2026-01-01, tolerance 0.1°. Target = the illumination center point computed for
2026-01-15 17:00 UTC (elevation 28.245°, azimuth 178.542°).

```
matches: [{ 2026-01-15 17:00, elevation 28.245012413710313°,
            azimuth 178.54216775316513°, angleDifferenceDegrees: 0 }]
```
Recovers the exact originating moment with zero angle difference - full round-trip
through `POST /api/solar/illuminate` → `POST /api/solar/alignments`, mirroring
`FindAlignments_RoundTrip_RecoversTheExactMomentThatIlluminatedTheTarget`. ✅

### 6. Corner-wraparound / multi-patch illumination
Covered by `RayTracerTests.Trace_WhenLightOverflowsPastThePrimarySurface_PopulatesMultiplePatches`
(still passing, unmodified) and by `Chiaroscuro.Api.Tests`' DTO-mapping tests, which
assert that `IlluminationResponse.illumination.patches` carries a multi-patch result
through the HTTP layer without loss. Not re-derived by hand here, since reproducing the
exact raw sun-elevation/azimuth fixture via a real lat/long/date/time is unnecessary
duplication of what those automated tests already cover.

## Outstanding before Phase 6 sign-off

- **Packaged Electron build:** win-x64 built via `electron-builder --win --dir` and
  smoke-tested (`/health`, a live `/api/solar/illuminate` call, and a graceful
  `before-quit` shutdown that tree-kills the sidecar with no orphaned processes) -
  passing as of 2026-08-19. linux-x64/osx-x64/osx-arm64 packaged builds were **not**
  attempted in this environment (Windows-only dev machine) - do that as part of Phase 7's
  CI rework (matrix build), or manually on the relevant OS, before relying on this
  checklist as full multi-platform sign-off.
