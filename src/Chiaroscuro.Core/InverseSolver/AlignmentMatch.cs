using Chiaroscuro.Core.Solar;
using NodaTime;

namespace Chiaroscuro.Core.InverseSolver;

/// <summary>One timestamp at which the sun's direction lines up with a target point, within tolerance.</summary>
/// <param name="DateTime">The local (zone-aware) date and time of the match.</param>
/// <param name="SunPosition">The sun's elevation/azimuth at that moment.</param>
/// <param name="AngleDifferenceDegrees">
/// How far off the sun's actual direction was from a perfect hit on the target - 0 would be
/// an exact match, and this will always be less than or equal to the search's tolerance.
/// </param>
public readonly record struct AlignmentMatch(ZonedDateTime DateTime, SolarPosition SunPosition, double AngleDifferenceDegrees);
