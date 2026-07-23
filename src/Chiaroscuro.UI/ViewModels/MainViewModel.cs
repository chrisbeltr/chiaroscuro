using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.Solar;
using CommunityToolkit.Mvvm.ComponentModel;
using NodaTime;

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

    // --- Result ------------------------------------------------------------------------
    [ObservableProperty]
    private string _resultText = string.Empty;

    public MainViewModel()
    {
        Recalculate();
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

        var room = new Room((double)roomWidth, (double)roomLength, (double)roomHeight);
        var window = new Window(WindowWall, (double)windowHorizontalOffset, (double)windowSillHeight, (double)windowWidth, (double)windowHeight);
        var traceResult = RayTracer.Trace(room, window, sunPosition);

        ResultText = traceResult is { } hit
            ? $"Sun elevation {sunPosition.ElevationDegrees:F1}°, azimuth {sunPosition.AzimuthDegrees:F1}°\n"
              + $"Light lands on {hit.Surface} at ({hit.CenterPoint.X:F2}, {hit.CenterPoint.Y:F2}, {hit.CenterPoint.Z:F2})"
            : $"Sun elevation {sunPosition.ElevationDegrees:F1}°, azimuth {sunPosition.AzimuthDegrees:F1}°\n"
              + "No surface is illuminated at this time.";
    }
}
