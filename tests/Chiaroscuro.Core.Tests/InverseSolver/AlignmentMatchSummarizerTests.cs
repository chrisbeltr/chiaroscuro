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
