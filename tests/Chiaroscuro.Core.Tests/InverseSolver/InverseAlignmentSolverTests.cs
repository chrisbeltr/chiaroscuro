using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.InverseSolver;
using Chiaroscuro.Core.Solar;
using NodaTime;
using Xunit;

namespace Chiaroscuro.Core.Tests.InverseSolver;

public class InverseAlignmentSolverTests
{
    // Shared scenario: a South-facing window, confirmed (via a scratch run of RayTracer)
    // to actually illuminate the floor at 2026-01-15 17:00 UTC for this lat/long.
    private static readonly Room TestRoom = new(Width: 6, Length: 5, Height: 3);
    private static readonly Window TestWindow = new(WallOrientation.South, HorizontalOffset: 0, SillHeight: 1, Width: 1.2, Height: 1.5);
    private const double Latitude = 40.7128;
    private const double Longitude = -74.0060;
    private static readonly LocalDate SweepStartDate = new(2026, 1, 1);

    [Fact]
    public void FindAlignments_RoundTrip_RecoversTheExactMomentThatIlluminatedTheTarget()
    {
        // Forward direction: pick a known moment, ask RayTracer where the light lands.
        var knownDateTime = new LocalDateTime(2026, 1, 15, 17, 0, 0).InZoneStrictly(DateTimeZone.Utc);
        var knownSunPosition = SolarCalculator.Calculate(Latitude, Longitude, knownDateTime);
        var traceResult = RayTracer.Trace(TestRoom, TestWindow, knownSunPosition);
        Assert.NotNull(traceResult); // sanity check on the fixture itself

        // Reverse direction: hand that landing point back to the solver and confirm it
        // recovers the same moment. This is what actually proves the Sv ≈ Dv sign
        // convention documented on InverseAlignmentSolver is correct, rather than just
        // asserting it by inspection.
        var targetPoint = traceResult.Value.CenterPoint;
        var matches = InverseAlignmentSolver.FindAlignments(
            TestRoom, TestWindow, targetPoint,
            Latitude, Longitude, DateTimeZone.Utc,
            SweepStartDate, toleranceDegrees: 0.1);

        Assert.Contains(matches, match => match.DateTime.ToInstant() == knownDateTime.ToInstant());
    }

    [Fact]
    public void FindAlignments_EveryReturnedMatch_IsWithinTheRequestedTolerance()
    {
        var targetPoint = KnownIlluminatedPoint();
        const double tolerance = 3.0;

        var matches = InverseAlignmentSolver.FindAlignments(
            TestRoom, TestWindow, targetPoint,
            Latitude, Longitude, DateTimeZone.Utc,
            SweepStartDate, tolerance);

        Assert.NotEmpty(matches);
        Assert.All(matches, match => Assert.True(match.AngleDifferenceDegrees <= tolerance));
    }

    [Fact]
    public void FindAlignments_TighterTolerance_NeverFindsMoreMatchesThanLooserTolerance()
    {
        var targetPoint = KnownIlluminatedPoint();

        var looseMatches = InverseAlignmentSolver.FindAlignments(
            TestRoom, TestWindow, targetPoint, Latitude, Longitude, DateTimeZone.Utc, SweepStartDate, toleranceDegrees: 5.0);
        var tightMatches = InverseAlignmentSolver.FindAlignments(
            TestRoom, TestWindow, targetPoint, Latitude, Longitude, DateTimeZone.Utc, SweepStartDate, toleranceDegrees: 0.05);

        Assert.True(tightMatches.Count <= looseMatches.Count);
    }

    private static Vector3 KnownIlluminatedPoint()
    {
        var dateTime = new LocalDateTime(2026, 1, 15, 17, 0, 0).InZoneStrictly(DateTimeZone.Utc);
        var sunPosition = SolarCalculator.Calculate(Latitude, Longitude, dateTime);
        var traceResult = RayTracer.Trace(TestRoom, TestWindow, sunPosition);
        Assert.NotNull(traceResult);
        return traceResult.Value.CenterPoint;
    }
}
