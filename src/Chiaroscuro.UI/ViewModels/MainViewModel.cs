using Chiaroscuro.Core.Solar;
using CommunityToolkit.Mvvm.ComponentModel;
using NodaTime;

namespace Chiaroscuro.UI.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // [ObservableProperty] on a private field makes the CommunityToolkit.Mvvm source
    // generator write a public "StatusText" property for us (in a generated partial
    // class file, hence MainViewModel being declared "partial" below). Any XAML bound
    // to {Binding StatusText} will refresh automatically whenever this field changes.
    [ObservableProperty]
    private string _statusText = "Loading solar position...";

    public MainViewModel()
    {
        // Temporary placeholder location (New York City) just to prove the UI project
        // can call straight into Chiaroscuro.Core - the location picker comes later.
        var now = SystemClock.Instance.GetCurrentInstant().InUtc();
        var position = SolarCalculator.Calculate(latitudeDegrees: 40.7128, longitudeDegrees: -74.0060, now);

        StatusText = $"Sun elevation: {position.ElevationDegrees:F1}°, azimuth: {position.AzimuthDegrees:F1}°";
    }
}
