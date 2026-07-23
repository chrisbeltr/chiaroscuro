using Chiaroscuro.Core.Solar;
using NodaTime;
using Xunit;

namespace Chiaroscuro.Core.Tests.Solar;

public class SolarCalculatorTests
{
    private const double EarthAxialTiltDegrees = 23.44;

    [Fact]
    public void Elevation_AtNorthPole_OnJuneSolstice_EqualsAxialTilt()
    {
        var dateTime = new LocalDateTime(2024, 6, 21, 12, 0).InUtc();

        var position = SolarCalculator.Calculate(latitudeDegrees: 90.0, longitudeDegrees: 0, dateTime);

        Assert.Equal(EarthAxialTiltDegrees, position.ElevationDegrees, precision: 0);
    }

    [Fact]
    public void Elevation_AtSouthPole_OnDecemberSolstice_EqualsAxialTilt()
    {
        var dateTime = new LocalDateTime(2024, 12, 21, 12, 0).InUtc();

        var position = SolarCalculator.Calculate(latitudeDegrees: -90.0, longitudeDegrees: 0, dateTime);

        Assert.Equal(EarthAxialTiltDegrees, position.ElevationDegrees, precision: 0);
    }

    [Theory]
    [InlineData(45.0, 180.0)]
    [InlineData(-30.0, 90.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(89.9, 359.0)]
    public void ToUnitVector_AlwaysProducesNormalizedVector(double elevationDegrees, double azimuthDegrees)
    {
        var position = new SolarPosition(elevationDegrees, azimuthDegrees);

        var unitVector = position.ToUnitVector();

        Assert.Equal(1.0, unitVector.Length, precision: 9);
    }

    [Fact]
    public void Calculate_ReturnsValuesWithinPhysicallyValidRanges()
    {
        var dateTime = new LocalDateTime(2026, 3, 15, 18, 30).InUtc();

        var position = SolarCalculator.Calculate(latitudeDegrees: 40.7128, longitudeDegrees: -74.0060, dateTime);

        Assert.InRange(position.ElevationDegrees, -90.0, 90.0);
        Assert.InRange(position.AzimuthDegrees, 0.0, 360.0);
    }
}
