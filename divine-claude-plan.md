# Chiaroscuro — Project Bootstrap & Phase 1 Plan

## Context
`SPEC.md` defines Chiaroscuro, a cross-platform .NET 10 / Avalonia UI 11 solar
ray-tracking app: given a location, date, and time, compute the sun's
direction vector and project it as a ray into a modeled room to find where
light lands, plus an inverse solver that sweeps the year to find alignment
dates for a target point. The directory is currently empty aside from
`SPEC.md` — no solution, no code. This plan scaffolds the solution structure
and implements Phase 1 (Core Domain & Solar Library) per the spec's roadmap.

Decisions made with the user during spec review:
- **3D render engine** (SkiaSharp vs Silk.NET vs OpenTK): deferred to Phase 3.
  Not a blocker for Phase 1/2 since `Chiaroscuro.Core` and the Avalonia theme
  have no rendering dependency.
- **Platform heads**: Desktop + WASM only for now. No Android/iOS projects
  scaffolded (roadmap doesn't schedule mobile work; add later if needed).
- **Room model**: single rectangular room (W × L × H) with a single window
  aperture, matching the UI mockup exactly. No multi-window/arbitrary-polygon
  support needed now.
- **Persistence**: none. Session-only state, no save/load/serialization.

## Solution structure to create

```
Chiaroscuro.sln
Directory.Build.props            # shared TargetFramework/LangVersion/Nullable
src/
  Chiaroscuro.Core/               # net10.0 class library, zero UI deps
    Solar/                        # elevation/azimuth calc, sun unit vector
    Geometry/                     # ray-plane intersection, aperture projection
    InverseSolver/                # year-sweep alignment matcher
  Chiaroscuro.UI/                 # Avalonia class library (Views/ViewModels/Styles)
  Chiaroscuro.Desktop/             # net10.0 head, Program.cs entry point
  Chiaroscuro.Wasm/                # net10.0 browser-wasm head
tests/
  Chiaroscuro.Core.Tests/          # xUnit, targets 100% coverage per spec
```

## Phase 1 implementation (this pass)

1. **Scaffold solution & projects**
   - `dotnet new sln -n Chiaroscuro`
   - `Chiaroscuro.Core` as `net10.0` classlib, `Nullable=enable`, add `NodaTime` package.
   - `Chiaroscuro.Core.Tests` as `xunit` test project referencing Core.
   - Add `Directory.Build.props` at repo root for shared `LangVersion` (13) and nullable settings.
   - `dotnet sln add` all projects.

2. **Solar Direction Vector** (`Chiaroscuro.Core/Solar`)
   - Implement solar position algorithm (elevation `α`, azimuth `θ`) from
     latitude, longitude, `NodaTime` `ZonedDateTime`. Use a standard
     astronomical algorithm (NREL SPA or Meeus low-precision formulas —
     Meeus is sufficient for architectural-scale accuracy and much simpler
     to implement/test).
   - Compute sun unit vector `S_v` per spec §3.1 formula.
   - Unit tests: known solar-noon/solstice reference values at fixed
     lat/long (verifiable against published NOAA solar calculator figures).

3. **Ray-Plane Intersection** (`Chiaroscuro.Core/Geometry`)
   - `Room` model: width/length/height, origin-centered, True-North aligned.
   - `Window` aperture model: position + 2D polygon (rectangle) on a wall.
   - Ray equation `P(t) = W_center + t·(-S_v)`; intersect against floor and
     interior wall planes (spec §3.2).
   - Aperture polygon projection: project the window's 2D polygon along
     `-S_v` onto the intersected surface to produce the illuminated polygon.
   - Unit tests: straight-down sun (elevation 90°) hits floor center directly
     below window; grazing angles hit walls not floor; polygon shape/area
     sanity checks.

4. **Inverse Alignment Solver** (`Chiaroscuro.Core/InverseSolver`)
   - Given target point `T` and window `W`, compute `D_v = (W - T)/‖W - T‖`.
   - Sweep 365 days × 15-minute intervals, comparing `S_v` to `-D_v` within
     threshold angle `ε` (configurable), return matching timestamps.
   - Unit tests: construct a scenario with a known analytically-derivable
     match window and assert the solver finds it.

5. **Coverage check**: run `dotnet test /p:CollectCoverage=true` (coverlet)
   and confirm Core is at/near 100% per the spec's explicit Phase 1 bar.

## Not in this pass (later phases, noted for continuity)
- Phase 2: `Chiaroscuro.UI` Avalonia shell + dark-purple/amber theme
  (`#0C0A1D`/`#120E2A`/`#2E235A` backgrounds, `#F59E0B`/`#FBBF24`/`#FDE047`
  reserved strictly for light-related elements), MVVM view models via
  CommunityToolkit.Mvvm.
- Phase 3: 3D viewport — render engine choice made at that point.
- Phase 4: Inverse solver UI/timeline view.
- Phase 5: WASM head build + GitHub Actions CI/CD.

## Verification
- `dotnet build` succeeds for the solution.
- `dotnet test` passes all Core unit tests.
- Coverage report shows Phase 1 math logic (Solar/Geometry/InverseSolver) at
  100% or documents any deliberate gap.
