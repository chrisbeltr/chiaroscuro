# Phase 4: Inverse Alignment Solver UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose `Chiaroscuro.Core`'s already-built inverse alignment solver through the UI: a target-point/tolerance input, a reactive "top matching dates" timeline of Golden Highlight Cards with click-to-jump, and a target/tolerance visual indicator in the 3D viewport.

**Architecture:** Two small, independent additions to `Chiaroscuro.Core` (`Vector3.Cross`, `AlignmentMatchSummarizer`), then three UI-layer pieces built on top of them: `SceneBuilder`/`RoomViewport` gain a target-crosshair-and-tolerance-ring renderer, `MainViewModel` gains the reactive solver wiring and a small `AlignmentMatchCard` display type, and `MainWindow.axaml` gains the input fields and results cards.

**Tech Stack:** No new dependencies - `NodaTime` (already used throughout `Chiaroscuro.Core`/`InverseSolver`), `CommunityToolkit.Mvvm` (`[ObservableProperty]`, already used everywhere in `MainViewModel` - this plan does not need `[RelayCommand]`; see Task 4).

## Global Constraints

- Target framework `net10.0`, `Nullable` enabled, `LangVersion` 13 - inherited automatically from `Directory.Build.props`.
- Color palette: the target marker and tolerance ring use the existing wireframe violet (`WireframeColor` in `SceneBuilder.cs`, `#9491C0`) - **not** any amber/gold/yellow shade, per this feature's design review (those colors are reserved for real light per `SPEC.md` §1). The results cards use the theme's already-defined-but-currently-unused `ChiaroscuroGoldBrush`/`ChiaroscuroGoldBackgroundBrush` resources (`#FBBF24`/`#402E00`) - `SPEC.md` calls that shade out specifically for "active control highlights."
- Fully reactive: every new input wires into the existing `OnXChanged → Recalculate()` pipeline in `MainViewModel` - no new buttons or explicit triggers anywhere in this plan.
- No new failure modes: the solver step only runs when both `TargetPoint` and `ToleranceDegrees` are available; when either is missing, the affected properties are left at the exact last-known-good/null state described in each task below - never an exception.

---

### Task 1: `Vector3.Cross`

**Files:**
- Modify: `src/Chiaroscuro.Core/Geometry/Vector3.cs`
- Test: `tests/Chiaroscuro.Core.Tests/Geometry/Vector3Tests.cs`

**Interfaces:**
- Produces: `Vector3.Cross(Vector3 other) -> Vector3` (instance method, standard 3D cross product).

- [ ] **Step 1: Write the failing tests**

Add these three `[Fact]` methods to the end of the `Vector3Tests` class in `tests/Chiaroscuro.Core.Tests/Geometry/Vector3Tests.cs` (just before the closing `}` of the class, after `ArithmeticOperators_BehaveComponentwise`):

```csharp
    [Fact]
    public void Cross_StandardBasisVectors_FollowsRightHandRule()
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);

        var result = x.Cross(y);

        Assert.Equal(new Vector3(0, 0, 1), result);
    }

    [Fact]
    public void Cross_IsAntiCommutative()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(4, 5, 6);

        Assert.Equal(a.Cross(b), -(b.Cross(a)));
    }

    [Fact]
    public void Cross_OfParallelVectors_IsZero()
    {
        var a = new Vector3(2, 4, 6);
        var b = new Vector3(1, 2, 3); // same direction as a, different magnitude

        Assert.Equal(Vector3.Zero, a.Cross(b));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: build error - `Vector3` does not contain a definition for `Cross`.

- [ ] **Step 3: Implement `Cross`**

In `src/Chiaroscuro.Core/Geometry/Vector3.cs`, add this method to the `Vector3` record struct, right after `Dot`:

```csharp
    /// <summary>The standard 3D cross product: a vector perpendicular to both inputs, with
    /// magnitude equal to the area of the parallelogram they span and direction given by the
    /// right-hand rule. Zero when the two vectors are parallel (including when either is
    /// itself zero).</summary>
    public Vector3 Cross(Vector3 other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: all tests pass, including the 3 new `Cross` tests and every pre-existing test in that project.

- [ ] **Step 5: Commit**

```bash
git add src/Chiaroscuro.Core/Geometry/Vector3.cs tests/Chiaroscuro.Core.Tests/Geometry/Vector3Tests.cs
git commit -m "Add Vector3.Cross"
```

---

### Task 2: `AlignmentMatchSummarizer`

**Files:**
- Create: `src/Chiaroscuro.Core/InverseSolver/AlignmentMatchSummarizer.cs`
- Test: `tests/Chiaroscuro.Core.Tests/InverseSolver/AlignmentMatchSummarizerTests.cs`

**Interfaces:**
- Consumes: `Chiaroscuro.Core.InverseSolver.AlignmentMatch` (existing - `record struct AlignmentMatch(ZonedDateTime DateTime, SolarPosition SunPosition, double AngleDifferenceDegrees)`).
- Produces: `AlignmentMatchSummarizer.SummarizeTopMatches(IReadOnlyList<AlignmentMatch> matches, int maxResults) -> IReadOnlyList<AlignmentMatch>`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Chiaroscuro.Core.Tests/InverseSolver/AlignmentMatchSummarizerTests.cs`:

```csharp
using Chiaroscuro.Core.InverseSolver;
using Chiaroscuro.Core.Solar;
using NodaTime;
using Xunit;

namespace Chiaroscuro.Core.Tests.InverseSolver;

public class AlignmentMatchSummarizerTests
{
    private static AlignmentMatch Match(int day, int hour, int minute, double angleDifferenceDegrees) =>
        new(new LocalDateTime(2026, 1, day, hour, minute, 0).InZoneStrictly(DateTimeZone.Utc),
            new SolarPosition(ElevationDegrees: 30, AzimuthDegrees: 180),
            angleDifferenceDegrees);

    [Fact]
    public void SummarizeTopMatches_MultipleMatchesOnTheSameDay_KeepsOnlyTheClosestOne()
    {
        AlignmentMatch[] matches =
        [
            Match(day: 5, hour: 9, minute: 0, angleDifferenceDegrees: 1.5),
            Match(day: 5, hour: 9, minute: 15, angleDifferenceDegrees: 0.2), // closest on day 5
            Match(day: 5, hour: 9, minute: 30, angleDifferenceDegrees: 1.0),
        ];

        var result = AlignmentMatchSummarizer.SummarizeTopMatches(matches, maxResults: 10);

        var match = Assert.Single(result);
        Assert.Equal(0.2, match.AngleDifferenceDegrees);
    }

    [Fact]
    public void SummarizeTopMatches_MoreDaysThanMaxResults_KeepsOnlyTheClosestDays()
    {
        AlignmentMatch[] matches =
        [
            Match(day: 1, hour: 9, minute: 0, angleDifferenceDegrees: 3.0),
            Match(day: 2, hour: 9, minute: 0, angleDifferenceDegrees: 0.1),
            Match(day: 3, hour: 9, minute: 0, angleDifferenceDegrees: 1.0),
        ];

        var result = AlignmentMatchSummarizer.SummarizeTopMatches(matches, maxResults: 2);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, match => match.AngleDifferenceDegrees == 3.0);
    }

    [Fact]
    public void SummarizeTopMatches_ReturnsResultsInChronologicalOrder_NotClosenessOrder()
    {
        AlignmentMatch[] matches =
        [
            Match(day: 10, hour: 9, minute: 0, angleDifferenceDegrees: 0.1), // closest, but latest date
            Match(day: 1, hour: 9, minute: 0, angleDifferenceDegrees: 2.0),  // furthest, but earliest date
        ];

        var result = AlignmentMatchSummarizer.SummarizeTopMatches(matches, maxResults: 10);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].DateTime.ToInstant() < result[1].DateTime.ToInstant());
    }

    [Fact]
    public void SummarizeTopMatches_EmptyInput_ReturnsEmpty()
    {
        var result = AlignmentMatchSummarizer.SummarizeTopMatches([], maxResults: 10);

        Assert.Empty(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: build error - `AlignmentMatchSummarizer` does not exist in the current context.

- [ ] **Step 3: Implement `AlignmentMatchSummarizer`**

Create `src/Chiaroscuro.Core/InverseSolver/AlignmentMatchSummarizer.cs`:

```csharp
namespace Chiaroscuro.Core.InverseSolver;

/// <summary>
/// Reduces <see cref="InverseAlignmentSolver.FindAlignments"/>'s raw, 15-minute-step matches
/// down to a clean, presentable timeline: one entry per calendar day (whichever match that
/// day is closest to a perfect alignment), keeping only the closest days overall, returned in
/// chronological order - so the app decides *which* days are worth showing by closeness, but
/// presents them as an actual timeline rather than a closeness-ranked list.
/// </summary>
public static class AlignmentMatchSummarizer
{
    public static IReadOnlyList<AlignmentMatch> SummarizeTopMatches(IReadOnlyList<AlignmentMatch> matches, int maxResults)
    {
        return matches
            .GroupBy(match => match.DateTime.Date)
            .Select(group => group.MinBy(match => match.AngleDifferenceDegrees))
            .OrderBy(best => best.AngleDifferenceDegrees)
            .Take(maxResults)
            .OrderBy(best => best.DateTime.ToInstant())
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chiaroscuro.Core.Tests/Chiaroscuro.Core.Tests.csproj`
Expected: all tests pass, including the 4 new `AlignmentMatchSummarizerTests` and every pre-existing test in that project.

- [ ] **Step 5: Commit**

```bash
git add src/Chiaroscuro.Core/InverseSolver/AlignmentMatchSummarizer.cs \
        tests/Chiaroscuro.Core.Tests/InverseSolver/AlignmentMatchSummarizerTests.cs
git commit -m "Add AlignmentMatchSummarizer"
```

---

### Task 3: Target/tolerance indicator in `SceneBuilder` and `RoomViewport`

**Files:**
- Modify: `src/Chiaroscuro.UI/Viewport/SceneBuilder.cs`
- Test: `tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs`
- Modify: `src/Chiaroscuro.UI/Views/RoomViewport.cs`

**Interfaces:**
- Consumes: `Vector3.Cross` (Task 1).
- Produces: `SceneBuilder.Build(Room room, Window window, IlluminationResult? illumination, Vector3? target = null, double? toleranceDegrees = null) -> IReadOnlyList<ScenePrimitive>` (the two new parameters are optional, defaulting to `null`, so every existing call site - all 5 in `SceneBuilderTests.cs` - keeps compiling unmodified). `RoomViewport.TargetPoint` (`Vector3?`, `StyledProperty`) and `RoomViewport.ToleranceDegrees` (`decimal?`, `StyledProperty` - `decimal?` to match `MainViewModel`'s type exactly for binding safety; converted to `double?` internally before being passed to `SceneBuilder.Build`).

- [ ] **Step 1: Write the failing tests**

Add these three `[Fact]` methods to the end of the `SceneBuilderTests` class in `tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs` (just before the closing `}` of the class, after `Build_WhenLightConeExtendsPastTheRoom_ClipsEachFaceToTheRoomBounds`):

```csharp
    [Fact]
    public void Build_WithTargetPoint_EmitsCrosshairLinesCenteredOnTheTarget()
    {
        var target = new Vector3(0, -1.5, 0.5);

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination: null, target: target);

        // 16 wireframe lines (unaffected) + 2 crosshair segments (no ring, since no tolerance
        // was given).
        var lines = primitives.OfType<SceneLine>().ToList();
        Assert.Equal(18, lines.Count);

        var crosshairLines = lines.Skip(16).ToList();
        Assert.All(crosshairLines, line =>
        {
            var midpoint = (line.Start + line.End) * 0.5;
            Assert.Equal(target.X, midpoint.X, precision: 9);
            Assert.Equal(target.Y, midpoint.Y, precision: 9);
            Assert.Equal(target.Z, midpoint.Z, precision: 9);
        });
    }

    [Fact]
    public void Build_WithTargetPointAndTolerance_AlsoEmitsARingAtTheExpectedRadius()
    {
        var target = new Vector3(0, -1.5, 0.5);
        const double toleranceDegrees = 5.0;

        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination: null, target, toleranceDegrees);

        var windowCenter = TestWindow.GetCenter(TestRoom);
        var expectedRadius = (windowCenter - target).Length * Math.Tan(double.DegreesToRadians(toleranceDegrees));

        // 16 wireframe + 2 crosshair + 32 ring segments.
        var lines = primitives.OfType<SceneLine>().ToList();
        Assert.Equal(50, lines.Count);

        var ringLines = lines.Skip(18).ToList();
        Assert.Equal(32, ringLines.Count);
        Assert.All(ringLines, line =>
        {
            Assert.Equal(expectedRadius, (line.Start - target).Length, precision: 6);
            Assert.Equal(expectedRadius, (line.End - target).Length, precision: 6);
        });
    }

    [Fact]
    public void Build_WithoutTargetPoint_EmitsNoIndicatorPrimitives()
    {
        var primitives = SceneBuilder.Build(TestRoom, TestWindow, illumination: null);

        Assert.Equal(16, primitives.Count); // just the wireframe, nothing else
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chiaroscuro.UI.Tests/Chiaroscuro.UI.Tests.csproj`
Expected: build error - `SceneBuilder.Build` has no overload taking 4 or 5 arguments.

- [ ] **Step 3: Implement the indicator in `SceneBuilder`**

In `src/Chiaroscuro.UI/Viewport/SceneBuilder.cs`, change the `Build` method's signature and body:

```csharp
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
```

Then add this new private method at the end of the class, right after `AddLightCone`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chiaroscuro.UI.Tests/Chiaroscuro.UI.Tests.csproj`
Expected: all tests pass, including the 3 new tests and every pre-existing test in that project.

- [ ] **Step 5: Wire `RoomViewport`'s new bindable properties**

In `src/Chiaroscuro.UI/Views/RoomViewport.cs`, add two new `StyledProperty`s right after `IlluminationProperty`:

```csharp
    public static readonly StyledProperty<Vector3?> TargetPointProperty =
        AvaloniaProperty.Register<RoomViewport, Vector3?>(nameof(TargetPoint));

    public static readonly StyledProperty<decimal?> ToleranceDegreesProperty =
        AvaloniaProperty.Register<RoomViewport, decimal?>(nameof(ToleranceDegrees));
```

and their CLR property wrappers right after the `Illumination` property:

```csharp
    public Vector3? TargetPoint
    {
        get => GetValue(TargetPointProperty);
        set => SetValue(TargetPointProperty, value);
    }

    public decimal? ToleranceDegrees
    {
        get => GetValue(ToleranceDegreesProperty);
        set => SetValue(ToleranceDegreesProperty, value);
    }
```

Change `OnPropertyChanged`'s `InvalidateVisual()` condition to also cover the two new properties:

```csharp
        if (change.Property == RoomProperty || change.Property == WindowProperty || change.Property == IlluminationProperty
            || change.Property == TargetPointProperty || change.Property == ToleranceDegreesProperty)
        {
            InvalidateVisual();
        }
```

Change `Render(DrawingContext context)` to pass the two new values through:

```csharp
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Snapshot the camera on the UI thread: Render() on the draw operation below runs on
        // Avalonia's render thread, while pointer/wheel handlers mutate _camera on this (UI)
        // thread. Passing a frozen copy instead of the live instance avoids reading a
        // torn/half-updated camera state (e.g. new yaw with old pitch) from another thread.
        var cameraSnapshot = new OrbitCamera(_camera.Target, _camera.Yaw, _camera.Pitch, _camera.Distance);
        context.Custom(new ViewportDrawOperation(
            new Rect(Bounds.Size), cameraSnapshot, Room, Window, Illumination, TargetPoint, (double?)ToleranceDegrees));
    }
```

Change the private `ViewportDrawOperation` class's primary constructor to accept the two new values, and pass them into `SceneBuilder.Build`:

```csharp
    private sealed class ViewportDrawOperation(
        Rect bounds, OrbitCamera camera, Room room, Window window, IlluminationResult? illumination,
        Vector3? target, double? toleranceDegrees)
        : ICustomDrawOperation
```

and inside that class's `Render(ImmediateDrawingContext context)` method, change:

```csharp
            var primitives = SceneBuilder.Build(room, window, illumination);
```

to:

```csharp
            var primitives = SceneBuilder.Build(room, window, illumination, target, toleranceDegrees);
```

- [ ] **Step 6: Verify the whole solution builds**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Chiaroscuro.UI/Viewport/SceneBuilder.cs tests/Chiaroscuro.UI.Tests/Viewport/SceneBuilderTests.cs \
        src/Chiaroscuro.UI/Views/RoomViewport.cs
git commit -m "Add target/tolerance indicator to the 3D viewport"
```

---

### Task 4: `MainViewModel` solver wiring

**Files:**
- Create: `src/Chiaroscuro.UI/ViewModels/AlignmentMatchCard.cs`
- Modify: `src/Chiaroscuro.UI/ViewModels/MainViewModel.cs`

**Interfaces:**
- Consumes: `AlignmentMatchSummarizer.SummarizeTopMatches` (Task 2); `Chiaroscuro.Core.InverseSolver.InverseAlignmentSolver.FindAlignments` (existing).
- Produces: `AlignmentMatchCard(string DateLabel, string TimeLabel, string AngleLabel, DateTime DateTime)` (a plain `record`, not tied to Avalonia). New `MainViewModel` properties: `TargetX`, `TargetY`, `TargetZ`, `ToleranceDegrees` (all `decimal?`), `TargetPoint` (`Vector3?`), `AlignmentMatches` (`IReadOnlyList<AlignmentMatchCard>`), `SelectedAlignmentMatch` (`AlignmentMatchCard?`). Task 5's XAML binds to all of these by name.

This task has no dedicated automated test - `MainViewModel` isn't unit tested anywhere in this project (verified manually instead, same as Phases 2-3). Its steps below are "implement, then verify the whole solution still builds and every existing test still passes" rather than TDD red/green.

- [ ] **Step 1: Create `AlignmentMatchCard`**

Create `src/Chiaroscuro.UI/ViewModels/AlignmentMatchCard.cs`:

```csharp
namespace Chiaroscuro.UI.ViewModels;

/// <summary>One "Golden Highlight Card" in the inverse solver's results strip - a single
/// alignment match, pre-formatted for display. Kept separate from
/// <see cref="Chiaroscuro.Core.InverseSolver.AlignmentMatch"/> so XAML bindings never need to
/// format NodaTime types directly (matching how <see cref="MainViewModel.ResultText"/> is
/// already a pre-formatted string rather than something bound through converters).</summary>
/// <param name="DateLabel">The match's date, formatted for display (e.g. "Mar 15").</param>
/// <param name="TimeLabel">The match's time, formatted for display (e.g. "2:45 PM").</param>
/// <param name="AngleLabel">How close the match was, formatted for display (e.g. "0.30° off").</param>
/// <param name="DateTime">
/// The match's local wall-clock date and time, unformatted - used by
/// <c>MainViewModel.OnSelectedAlignmentMatchChanged</c> to jump the app's Date/TimeOfDay to
/// this match when the card is clicked, without having to re-parse the display strings.
/// </param>
public sealed record AlignmentMatchCard(string DateLabel, string TimeLabel, string AngleLabel, DateTime DateTime);
```

- [ ] **Step 2: Replace `MainViewModel.cs`**

Replace the full contents of `src/Chiaroscuro.UI/ViewModels/MainViewModel.cs` with:

```csharp
using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.InverseSolver;
using Chiaroscuro.Core.Solar;
using CommunityToolkit.Mvvm.ComponentModel;
using NodaTime;
using System.Globalization;

namespace Chiaroscuro.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // Static, never changes, so it's a plain property (not [ObservableProperty]) - it just
    // gives the Wall ComboBox in XAML something to list via ItemsSource.
    public static IReadOnlyList<WallOrientation> WallOrientations { get; } = Enum.GetValues<WallOrientation>();

    // --- Location & time -----------------------------------------------------------
    // Defaults to New York City, matched against suncalc.org during manual testing.
    [ObservableProperty]
    private decimal? _latitude = 40.7128m;

    [ObservableProperty]
    private decimal? _longitude = -74.0060m;

    // DatePicker/TimePicker bind to these exact types (DateTimeOffset?/TimeSpan?).
    [ObservableProperty]
    private DateTimeOffset? _date = DateTimeOffset.Now.Date;

    [ObservableProperty]
    private TimeSpan? _timeOfDay = DateTimeOffset.Now.TimeOfDay;

    // A raw UTC offset (e.g. -5 for EST) rather than a named timezone - SolarCalculator only
    // ever needs the exact UTC instant, so this is enough without a full IANA zone picker.
    [ObservableProperty]
    private decimal? _utcOffsetHours = -4m;

    // --- Room ------------------------------------------------------------------------
    [ObservableProperty]
    private decimal? _roomWidth = 6m;

    [ObservableProperty]
    private decimal? _roomLength = 5m;

    [ObservableProperty]
    private decimal? _roomHeight = 3m;

    // --- Window ------------------------------------------------------------------------
    // South is an arbitrary starting default, not a spec requirement.
    [ObservableProperty]
    private WallOrientation _windowWall = WallOrientation.South;

    [ObservableProperty]
    private decimal? _windowHorizontalOffset = 0m;

    [ObservableProperty]
    private decimal? _windowSillHeight = 1m;

    [ObservableProperty]
    private decimal? _windowWidth = 1.2m;

    [ObservableProperty]
    private decimal? _windowHeight = 1.5m;

    // --- Inverse solver target -------------------------------------------------------
    // Seeded once, in the constructor, from wherever the sun initially lands (or the floor
    // center if nothing is illuminated yet) - after that, purely user-controlled. See the
    // constructor below.
    [ObservableProperty]
    private decimal? _targetX;

    [ObservableProperty]
    private decimal? _targetY;

    [ObservableProperty]
    private decimal? _targetZ;

    [ObservableProperty]
    private decimal? _toleranceDegrees = 2m;

    // Null whenever any of TargetX/Y/Z is empty - RoomViewport binds to this directly to draw
    // (or hide) the target crosshair/tolerance ring.
    [ObservableProperty]
    private Vector3? _targetPoint;

    [ObservableProperty]
    private IReadOnlyList<AlignmentMatchCard> _alignmentMatches = [];

    // Bound to the results ListBox's SelectedItem - clicking a card jumps Date/TimeOfDay to
    // that match, via OnSelectedAlignmentMatchChanged below.
    [ObservableProperty]
    private AlignmentMatchCard? _selectedAlignmentMatch;

    // --- Result ------------------------------------------------------------------------
    [ObservableProperty]
    private string _resultText = string.Empty;

    // The 3D viewport (RoomViewport) binds to these three directly, so it always shows
    // exactly what Recalculate() just computed. Room defaults to a sensible non-empty box
    // so the viewport has something to render even before Recalculate() runs for the first
    // time (though in practice that happens immediately, in the constructor below).
    [ObservableProperty]
    private Room _room = new(6, 5, 3);

    [ObservableProperty]
    private Window _window;

    [ObservableProperty]
    private IlluminationResult? _illumination;

    public MainViewModel()
    {
        Recalculate();

        // TargetX/Y/Z default to wherever the sun is illuminating right now, so the inverse
        // solver has a sensible starting point instead of an arbitrary (0,0,0). This has to
        // happen after the Recalculate() call above, since Illumination doesn't exist until
        // that's run once. Setting these three trigger their own Recalculate() calls in turn
        // (via OnTargetXChanged etc. below), which is what actually populates TargetPoint and
        // runs the solver for the first time.
        var seed = Illumination?.CenterPoint ?? new Vector3(0, 0, 0);
        TargetX = (decimal)seed.X;
        TargetY = (decimal)seed.Y;
        TargetZ = (decimal)seed.Z;
    }

    // One partial "OnXChanged" hook per input field - CommunityToolkit's generator calls
    // whichever of these it finds a matching [ObservableProperty] for, immediately after
    // that property's setter runs. They all just funnel into the same recalculation.
    partial void OnLatitudeChanged(decimal? value) => Recalculate();
    partial void OnLongitudeChanged(decimal? value) => Recalculate();
    partial void OnDateChanged(DateTimeOffset? value) => Recalculate();
    partial void OnTimeOfDayChanged(TimeSpan? value) => Recalculate();
    partial void OnUtcOffsetHoursChanged(decimal? value) => Recalculate();
    partial void OnRoomWidthChanged(decimal? value) => Recalculate();
    partial void OnRoomLengthChanged(decimal? value) => Recalculate();
    partial void OnRoomHeightChanged(decimal? value) => Recalculate();
    partial void OnWindowWallChanged(WallOrientation value) => Recalculate();
    partial void OnWindowHorizontalOffsetChanged(decimal? value) => Recalculate();
    partial void OnWindowSillHeightChanged(decimal? value) => Recalculate();
    partial void OnWindowWidthChanged(decimal? value) => Recalculate();
    partial void OnWindowHeightChanged(decimal? value) => Recalculate();
    partial void OnTargetXChanged(decimal? value) => Recalculate();
    partial void OnTargetYChanged(decimal? value) => Recalculate();
    partial void OnTargetZChanged(decimal? value) => Recalculate();
    partial void OnToleranceDegreesChanged(decimal? value) => Recalculate();

    partial void OnSelectedAlignmentMatchChanged(AlignmentMatchCard? value)
    {
        if (value is null)
        {
            return;
        }

        Date = new DateTimeOffset(value.DateTime.Date, TimeSpan.Zero);
        TimeOfDay = value.DateTime.TimeOfDay;
    }

    private void Recalculate()
    {
        // Nullable pattern matching: "is not { } name" both checks for a non-null value AND
        // unwraps it into a new non-nullable local called "name", all in one condition. If
        // any field is empty (user cleared a NumericUpDown), bail out instead of calculating
        // against a missing value.
        if (Latitude is not { } latitude ||
            Longitude is not { } longitude ||
            Date is not { } date ||
            TimeOfDay is not { } timeOfDay ||
            UtcOffsetHours is not { } utcOffsetHours ||
            RoomWidth is not { } roomWidth ||
            RoomLength is not { } roomLength ||
            RoomHeight is not { } roomHeight ||
            WindowHorizontalOffset is not { } windowHorizontalOffset ||
            WindowSillHeight is not { } windowSillHeight ||
            WindowWidth is not { } windowWidth ||
            WindowHeight is not { } windowHeight)
        {
            ResultText = "Enter all parameters to calculate.";
            return;
        }

        var localDate = new LocalDate(date.Year, date.Month, date.Day);
        var localTime = LocalTime.FromTicksSinceMidnight(timeOfDay.Ticks);
        var localDateTime = localDate.At(localTime);
        var offset = Offset.FromTimeSpan(TimeSpan.FromHours((double)utcOffsetHours));
        var instant = new OffsetDateTime(localDateTime, offset).ToInstant();

        var sunPosition = SolarCalculator.Calculate((double)latitude, (double)longitude, instant.InUtc());

        Room = new Room((double)roomWidth, (double)roomLength, (double)roomHeight);
        Window = new Window(WindowWall, (double)windowHorizontalOffset, (double)windowSillHeight, (double)windowWidth, (double)windowHeight);
        Illumination = RayTracer.Trace(Room, Window, sunPosition);

        ResultText = Illumination is { } hit
            ? $"Sun elevation {sunPosition.ElevationDegrees:F1}°, azimuth {sunPosition.AzimuthDegrees:F1}°\n"
              + $"Light lands on {hit.Surface} at ({hit.CenterPoint.X:F2}, {hit.CenterPoint.Y:F2}, {hit.CenterPoint.Z:F2})"
            : $"Sun elevation {sunPosition.ElevationDegrees:F1}°, azimuth {sunPosition.AzimuthDegrees:F1}°\n"
              + "No surface is illuminated at this time.";

        // The target point is independent of whether a tolerance is set - it always reflects
        // the raw X/Y/Z fields, so RoomViewport can draw the crosshair even without a ring.
        TargetPoint = TargetX is { } targetX && TargetY is { } targetY && TargetZ is { } targetZ
            ? new Vector3((double)targetX, (double)targetY, (double)targetZ)
            : null;

        // The solver only runs when both a target and a tolerance are available. If either
        // is missing, AlignmentMatches is simply left at its last-known-good value, the same
        // way Room/Window/Illumination are left alone when other optional input is missing.
        if (TargetPoint is { } target && ToleranceDegrees is { } toleranceDegrees)
        {
            var zone = DateTimeZone.ForOffset(offset);
            var rawMatches = InverseAlignmentSolver.FindAlignments(
                Room, Window, target, (double)latitude, (double)longitude, zone, localDate, (double)toleranceDegrees);
            var topMatches = AlignmentMatchSummarizer.SummarizeTopMatches(rawMatches, maxResults: 15);

            AlignmentMatches = topMatches.Select(match =>
            {
                var matchDateTime = match.DateTime.ToDateTimeUnspecified();
                return new AlignmentMatchCard(
                    matchDateTime.ToString("MMM d", CultureInfo.InvariantCulture),
                    matchDateTime.ToString("h:mm tt", CultureInfo.InvariantCulture),
                    $"{match.AngleDifferenceDegrees:F2}° off",
                    matchDateTime);
            }).ToList();
        }
    }
}
```

- [ ] **Step 3: Verify the whole solution builds and every existing test still passes**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

Run: `dotnet test`
Expected: `Passed!` for both `Chiaroscuro.Core.Tests` and `Chiaroscuro.UI.Tests`, 0 failures (this task adds no new automated tests, so the count should match Task 3's ending count exactly).

- [ ] **Step 4: Commit**

```bash
git add src/Chiaroscuro.UI/ViewModels/AlignmentMatchCard.cs src/Chiaroscuro.UI/ViewModels/MainViewModel.cs
git commit -m "Wire the inverse alignment solver into MainViewModel"
```

---

### Task 5: `MainWindow.axaml` UI

**Files:**
- Modify: `src/Chiaroscuro.UI/Views/MainWindow.axaml`
- Modify: `src/Chiaroscuro.UI/Themes/ChiaroscuroTheme.axaml`

**Interfaces:**
- Consumes: `RoomViewport.TargetPoint`/`ToleranceDegrees` (Task 3); `MainViewModel.TargetX`/`TargetY`/`TargetZ`/`ToleranceDegrees`/`AlignmentMatches`/`SelectedAlignmentMatch` (Task 4); `AlignmentMatchCard.DateLabel`/`TimeLabel`/`AngleLabel` (Task 4).

No automated test - XAML/layout changes in this project are verified by building successfully and manual visual inspection, the same as every prior XAML change in Phases 2-3.

- [ ] **Step 1: Add the `matchCard` and `ListBoxItem` styles**

In `src/Chiaroscuro.UI/Themes/ChiaroscuroTheme.axaml`, add these two styles right after the existing `Border.panel` style, before the closing `</Styles>` tag:

```xml
    <!-- The inverse solver's "Golden Highlight Cards" - gold accent per SPEC.md's rule that
         that shade is for active control highlights/key UI focal points. -->
    <Style Selector="Border.matchCard">
        <Setter Property="Background" Value="{DynamicResource ChiaroscuroGoldBackgroundBrush}" />
        <Setter Property="BorderBrush" Value="{DynamicResource ChiaroscuroGoldBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="6" />
    </Style>

    <!-- Strip FluentTheme's default ListBoxItem padding/background so the matchCard Border
         above provides the entire visual, instead of a second box appearing behind it. -->
    <Style Selector="ListBoxItem">
        <Setter Property="Padding" Value="0" />
        <Setter Property="Background" Value="Transparent" />
    </Style>
```

- [ ] **Step 2: Add the "Target Point" sidebar card**

In `src/Chiaroscuro.UI/Views/MainWindow.axaml`, add this new `Border` to the left sidebar's `StackPanel`, right after the existing "Window" card's closing `</Border>` and before the `StackPanel`'s closing `</StackPanel>`:

```xml
                <Border Classes="panel" Padding="12">
                    <StackPanel Spacing="8">
                        <TextBlock Text="Target Point" FontWeight="Bold" />

                        <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,Auto,Auto">
                            <TextBlock Grid.Row="0" Grid.Column="0" Text="X" VerticalAlignment="Center" Margin="0,0,8,4" />
                            <NumericUpDown Grid.Row="0" Grid.Column="1" Margin="0,0,0,4"
                                           Value="{Binding TargetX}" Increment="0.1" FormatString="F2" />

                            <TextBlock Grid.Row="1" Grid.Column="0" Text="Y" VerticalAlignment="Center" Margin="0,0,8,4" />
                            <NumericUpDown Grid.Row="1" Grid.Column="1" Margin="0,0,0,4"
                                           Value="{Binding TargetY}" Increment="0.1" FormatString="F2" />

                            <TextBlock Grid.Row="2" Grid.Column="0" Text="Z" VerticalAlignment="Center" Margin="0,0,8,4" />
                            <NumericUpDown Grid.Row="2" Grid.Column="1" Margin="0,0,0,4"
                                           Value="{Binding TargetZ}" Increment="0.1" FormatString="F2" />

                            <TextBlock Grid.Row="3" Grid.Column="0" Text="Tolerance (°)" VerticalAlignment="Center" Margin="0,0,8,0" />
                            <NumericUpDown Grid.Row="3" Grid.Column="1"
                                           Value="{Binding ToleranceDegrees}" Increment="0.1" FormatString="F2" />
                        </Grid>
                    </StackPanel>
                </Border>
```

- [ ] **Step 3: Bind `RoomViewport`'s new properties**

In the same file, change:

```xml
                <views:RoomViewport Room="{Binding Room}" Window="{Binding Window}" Illumination="{Binding Illumination}" />
```

to:

```xml
                <views:RoomViewport Room="{Binding Room}" Window="{Binding Window}" Illumination="{Binding Illumination}"
                                    TargetPoint="{Binding TargetPoint}" ToleranceDegrees="{Binding ToleranceDegrees}" />
```

- [ ] **Step 4: Add the results cards to the bottom strip**

In the same file, change the bottom results `Border`:

```xml
            <Border Grid.Row="1" Classes="panel" Margin="0,12,0,0" Padding="16">
                <TextBlock Text="{Binding ResultText}" TextWrapping="Wrap" VerticalAlignment="Top" />
            </Border>
```

to:

```xml
            <Border Grid.Row="1" Classes="panel" Margin="0,12,0,0" Padding="16">
                <StackPanel Spacing="12">
                    <TextBlock Text="{Binding ResultText}" TextWrapping="Wrap" VerticalAlignment="Top" />

                    <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
                        <ListBox ItemsSource="{Binding AlignmentMatches}" SelectedItem="{Binding SelectedAlignmentMatch}"
                                 Background="Transparent" BorderThickness="0">
                            <ListBox.ItemsPanel>
                                <ItemsPanelTemplate>
                                    <StackPanel Orientation="Horizontal" Spacing="8" />
                                </ItemsPanelTemplate>
                            </ListBox.ItemsPanel>
                            <ListBox.ItemTemplate>
                                <DataTemplate>
                                    <Border Classes="matchCard" Padding="8">
                                        <StackPanel Spacing="2">
                                            <TextBlock Text="{Binding DateLabel}" FontWeight="Bold" />
                                            <TextBlock Text="{Binding TimeLabel}" />
                                            <TextBlock Text="{Binding AngleLabel}" FontSize="11" />
                                        </StackPanel>
                                    </Border>
                                </DataTemplate>
                            </ListBox.ItemTemplate>
                        </ListBox>
                    </ScrollViewer>
                </StackPanel>
            </Border>
```

- [ ] **Step 5: Verify the whole solution builds**

Run: `dotnet build`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Run the full test suite one more time**

Run: `dotnet test`
Expected: `Passed!` for both projects, same counts as Task 4 (this task adds no automated tests).

- [ ] **Step 7: Commit**

```bash
git add src/Chiaroscuro.UI/Views/MainWindow.axaml src/Chiaroscuro.UI/Themes/ChiaroscuroTheme.axaml
git commit -m "Add target point, tolerance, and alignment results UI to MainWindow"
```
