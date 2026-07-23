using Chiaroscuro.Core.Solar;
using NodaTime;

var dateTime = SystemClock.Instance.GetCurrentInstant().InUtc();
var position = SolarCalculator.Calculate(latitudeDegrees: 40.7128, longitudeDegrees: -74.0060, dateTime);

Console.WriteLine($"Elevation: {position.ElevationDegrees:F2}°, Azimuth: {position.AzimuthDegrees:F2}°");
Console.WriteLine($"Sun unit vector: {position.ToUnitVector()}");
