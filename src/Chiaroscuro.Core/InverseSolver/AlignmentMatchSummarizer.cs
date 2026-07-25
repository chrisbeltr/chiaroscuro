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
