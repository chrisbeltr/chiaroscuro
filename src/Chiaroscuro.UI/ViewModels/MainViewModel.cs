using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.InverseSolver;
using Chiaroscuro.Core.Solar;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using System.Globalization;

namespace Chiaroscuro.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // Static, never changes, so it's a plain property (not [ObservableProperty]) - it just
    // gives the Wall ComboBox in XAML something to list via ItemsSource.
    public static IReadOnlyList<WallOrientation> WallOrientations { get; } = Enum.GetValues<WallOrientation>();

    // Chiaroscuro.UI is shared by both the desktop and browser heads and has no platform-specific
    // code of its own, but "current location" needs a different strategy per platform (the
    // browser's navigator.geolocation vs. an IP-based lookup on desktop, since .NET has no
    // built-in cross-platform GPS API). Rather than pulling platform code into this shared
    // library, each head sets this delegate once at startup (see Chiaroscuro.Desktop/Program.cs
    // and Chiaroscuro.Wasm/Program.cs); left null, the button below is simply a no-op.
    public static Func<Task<(double Latitude, double Longitude, double UtcOffsetHours)?>>? LocationProvider { get; set; }

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
        JumpToNow();
        _ = JumpToCurrentLocation();

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

    [RelayCommand]
    private void JumpToNow()
    {
        SelectedAlignmentMatch = null;

        var now = DateTimeOffset.Now;
        Date = now.Date;
        TimeOfDay = now.TimeOfDay;
        UtcOffsetHours = (decimal)now.Offset.TotalHours;
    }

    [RelayCommand]
    private async Task JumpToCurrentLocation()
    {
        if (LocationProvider is null)
        {
            return;
        }

        var location = await LocationProvider();
        if (location is { } current)
        {
            Latitude = (decimal)current.Latitude;
            Longitude = (decimal)current.Longitude;
            UtcOffsetHours = (decimal)current.UtcOffsetHours;
        }
    }

    partial void OnSelectedAlignmentMatchChanged(AlignmentMatchCard? value)
    {
        if (value?.DateTime is not { } d)
        {
            return;
        }
        
        Date = new DateTimeOffset(d.Date, TimeSpan.Zero);
        TimeOfDay = d.TimeOfDay;
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
            
            var tempMatches = topMatches.Count > 0 ? topMatches.Select(match =>
            {
                var matchDateTime = match.DateTime.ToDateTimeUnspecified();
                return new AlignmentMatchCard(
                    matchDateTime.ToString("MMM d", CultureInfo.InvariantCulture),
                    matchDateTime.ToString("h:mm tt", CultureInfo.InvariantCulture),
                    $"{match.AngleDifferenceDegrees:F2}° off",
                    matchDateTime);
            }).ToList() : [
                new AlignmentMatchCard(
                    "No matches found.",
                    "Try a higher tolerance or different position.",
                    "", null)
            ];

            if (!tempMatches.SequenceEqual(AlignmentMatches))
            {
                AlignmentMatches = tempMatches;
            }
        }
    }
}
