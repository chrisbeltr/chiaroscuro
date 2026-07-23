using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.Solar;
using NodaTime;

namespace Chiaroscuro.Core.InverseSolver;

/// <summary>
/// Implements spec §3.3: given a target point and a window, finds the dates/times across
/// a year when the sun's direction aligns closely enough with the target to illuminate it.
/// <para>
/// NOTE ON THE MATCHING CONDITION: the spec's prose states the condition as "Sv ≈ -Dv", but
/// that's inconsistent with the spec's own §3.1 (Sv definition) and §3.2 (ray equation
/// P(t) = W + t*(-Sv), already implemented in <see cref="RayTracer"/>). Substituting T for
/// P(t) in that ray equation and solving gives Sv = (W-T)/‖W-T‖ - which is exactly the
/// spec's own definition of Dv. So the internally-consistent condition, and the one this
/// solver actually uses, is Sv ≈ Dv (not -Dv). This is verified by
/// InverseAlignmentSolverTests' round-trip test: tracing a known sun position forward with
/// RayTracer, then feeding the resulting point back into this solver, recovers that same
/// sun position.
/// </para>
/// </summary>
public static class InverseAlignmentSolver
{
    private static readonly Duration StepDuration = Duration.FromMinutes(15);
    private static readonly Duration SweepDuration = Duration.FromDays(365);

    public static IReadOnlyList<AlignmentMatch> FindAlignments(
        Room room,
        Window window,
        Vector3 targetPoint,
        double latitudeDegrees,
        double longitudeDegrees,
        DateTimeZone timeZone,
        LocalDate startDate,
        double toleranceDegrees)
    {
        var windowCenter = window.GetCenter(room);
        // D_v per spec §3.3: the direction from the target point toward the window.
        var targetDirection = (windowCenter - targetPoint).Normalized();

        // InZoneLeniently: resolves the (rare) case where midnight on startDate doesn't
        // correspond to exactly one real instant in this zone (a DST transition can skip
        // or repeat a local time) by picking a sensible instant instead of throwing.
        var startInstant = startDate.AtMidnight().InZoneLeniently(timeZone).ToInstant();

        var matches = new List<AlignmentMatch>();

        for (var elapsed = Duration.Zero; elapsed < SweepDuration; elapsed += StepDuration)
        {
            var zonedDateTime = (startInstant + elapsed).InZone(timeZone);

            var sunPosition = SolarCalculator.Calculate(latitudeDegrees, longitudeDegrees, zonedDateTime);
            var angleDifferenceDegrees = double.RadiansToDegrees(sunPosition.ToUnitVector().AngleTo(targetDirection));

            if (angleDifferenceDegrees <= toleranceDegrees)
            {
                matches.Add(new AlignmentMatch(zonedDateTime, sunPosition, angleDifferenceDegrees));
            }
        }

        return matches;
    }
}
