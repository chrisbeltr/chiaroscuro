using NodaTime;

namespace Chiaroscuro.Core.Solar;

/// <summary>
/// Computes sun elevation/azimuth using the Meeus low-precision solar position
/// algorithm (the same formula chain behind NOAA's public solar calculator).
/// Accurate to roughly ±0.01°.
/// </summary>
public static class SolarCalculator
{
    private const double UnixEpochJulianDay = 2440587.5;
    private const double J2000JulianDay = 2451545.0;
    private const double JulianDaysPerCentury = 36525.0;

    public static SolarPosition Calculate(double latitudeDegrees, double longitudeDegrees, ZonedDateTime dateTime)
    {
        var instant = dateTime.ToInstant();
        var julianCentury = (ToJulianDay(instant) - J2000JulianDay) / JulianDaysPerCentury;

        var geomMeanLongSunDeg = NormalizeDegrees(280.46646 + julianCentury * (36000.76983 + julianCentury * 0.0003032));
        var geomMeanAnomSunDeg = 357.52911 + julianCentury * (35999.05029 - 0.0001537 * julianCentury);
        var eccentricityEarthOrbit = 0.016708634 - julianCentury * (0.000042037 + 0.0000001267 * julianCentury);

        var meanAnomRad = double.DegreesToRadians(geomMeanAnomSunDeg);
        var sunEqOfCenter = Math.Sin(meanAnomRad) * (1.914602 - julianCentury * (0.004817 + 0.000014 * julianCentury))
            + Math.Sin(2 * meanAnomRad) * (0.019993 - 0.000101 * julianCentury)
            + Math.Sin(3 * meanAnomRad) * 0.000289;

        var sunTrueLongDeg = geomMeanLongSunDeg + sunEqOfCenter;

        var omegaDeg = 125.04 - 1934.136 * julianCentury;
        var sunAppLongDeg = sunTrueLongDeg - 0.00569 - 0.00478 * Math.Sin(double.DegreesToRadians(omegaDeg));

        var meanObliqEclipticDeg = 23.0 + (26.0 + (21.448 - julianCentury * (46.815 + julianCentury * (0.00059 - julianCentury * 0.001813))) / 60.0) / 60.0;
        var obliqCorrDeg = meanObliqEclipticDeg + 0.00256 * Math.Cos(double.DegreesToRadians(omegaDeg));

        var sunDeclinationRad = Math.Asin(Math.Sin(double.DegreesToRadians(obliqCorrDeg)) * Math.Sin(double.DegreesToRadians(sunAppLongDeg)));

        var eqOfTimeMinutes = EquationOfTimeMinutes(julianCentury, geomMeanLongSunDeg, meanAnomRad, eccentricityEarthOrbit, obliqCorrDeg);

        var trueSolarTimeMinutes = (MinutesSinceMidnightUtc(instant) + eqOfTimeMinutes + 4 * longitudeDegrees) % 1440;
        if (trueSolarTimeMinutes < 0)
        {
            trueSolarTimeMinutes += 1440;
        }

        var hourAngleDeg = trueSolarTimeMinutes / 4.0 - 180.0;
        var hourAngleRad = double.DegreesToRadians(hourAngleDeg);
        var latitudeRad = double.DegreesToRadians(latitudeDegrees);

        var cosZenith = Math.Clamp(
            Math.Sin(latitudeRad) * Math.Sin(sunDeclinationRad) + Math.Cos(latitudeRad) * Math.Cos(sunDeclinationRad) * Math.Cos(hourAngleRad),
            -1.0, 1.0);
        var zenithRad = Math.Acos(cosZenith);
        var elevationDegrees = 90.0 - double.RadiansToDegrees(zenithRad);

        var azimuthDegrees = AzimuthDegrees(latitudeRad, sunDeclinationRad, zenithRad, hourAngleDeg);

        return new SolarPosition(elevationDegrees, azimuthDegrees);
    }

    private static double AzimuthDegrees(double latitudeRad, double sunDeclinationRad, double zenithRad, double hourAngleDeg)
    {
        var sinZenith = Math.Sin(zenithRad);
        if (Math.Abs(sinZenith) < 1e-9)
        {
            // Sun at zenith/nadir directly above or below the observer: azimuth is undefined.
            return 0.0;
        }

        var cosAzimuth = Math.Clamp(
            (Math.Sin(sunDeclinationRad) - Math.Sin(latitudeRad) * Math.Cos(zenithRad)) / (Math.Cos(latitudeRad) * sinZenith),
            -1.0, 1.0);
        var azimuthDegrees = double.RadiansToDegrees(Math.Acos(cosAzimuth));

        return hourAngleDeg > 0 ? 360.0 - azimuthDegrees : azimuthDegrees;
    }

    private static double EquationOfTimeMinutes(double julianCentury, double geomMeanLongSunDeg, double meanAnomRad, double eccentricityEarthOrbit, double obliqCorrDeg)
    {
        var y = Math.Pow(Math.Tan(double.DegreesToRadians(obliqCorrDeg) / 2.0), 2);
        var meanLongRad = double.DegreesToRadians(geomMeanLongSunDeg);

        return 4.0 * double.RadiansToDegrees(
            y * Math.Sin(2 * meanLongRad)
            - 2 * eccentricityEarthOrbit * Math.Sin(meanAnomRad)
            + 4 * eccentricityEarthOrbit * y * Math.Sin(meanAnomRad) * Math.Cos(2 * meanLongRad)
            - 0.5 * y * y * Math.Sin(4 * meanLongRad)
            - 1.25 * eccentricityEarthOrbit * eccentricityEarthOrbit * Math.Sin(2 * meanAnomRad));
    }

    private static double MinutesSinceMidnightUtc(Instant instant)
    {
        var utc = instant.InUtc();
        return utc.Hour * 60.0 + utc.Minute + utc.Second / 60.0;
    }

    private static double ToJulianDay(Instant instant)
    {
        var daysSinceUnixEpoch = (instant - Instant.FromUnixTimeSeconds(0)).TotalDays;
        return UnixEpochJulianDay + daysSinceUnixEpoch;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var result = degrees % 360.0;
        return result < 0 ? result + 360.0 : result;
    }
}
