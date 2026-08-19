using Chiaroscuro.Api.Contracts;
using Chiaroscuro.Api.Mapping;
using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.InverseSolver;
using Chiaroscuro.Core.Solar;
using NodaTime;

namespace Chiaroscuro.Api.Endpoints;

/// <summary>
/// Splits MainViewModel.Recalculate()'s single in-process pipeline into two independent
/// endpoints so the frontend can cache/debounce the (cheap) illumination trace separately
/// from the (comparatively expensive, ~35,000-iteration) alignment sweep, rather than
/// re-running both on every keystroke the way the old in-process ViewModel did for free.
/// Neither endpoint ports the old ResultText/AlignmentMatchCard *string formatting* -
/// that's presentation and is rebuilt client-side from these raw numeric responses.
/// </summary>
public static class SolarEndpoints
{
    public static void MapSolarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/solar");

        group.MapPost("/illuminate", (IlluminationRequest request) =>
        {
            var instant = TimeMapping.ToInstant(request.Year, request.Month, request.Day, request.Hour, request.Minute, request.UtcOffsetHours);
            var sunPosition = SolarCalculator.Calculate(request.Latitude, request.Longitude, instant.InUtc());

            var room = GeometryMapping.ToRoom(request.Room);
            var window = GeometryMapping.ToWindow(request.Window);
            var illumination = RayTracer.Trace(room, window, sunPosition);

            return Results.Ok(new IlluminationResponse(
                new SunPositionDto(sunPosition.ElevationDegrees, sunPosition.AzimuthDegrees),
                GeometryMapping.ToDto(illumination)));
        });

        group.MapPost("/alignments", (AlignmentsRequest request) =>
        {
            var room = GeometryMapping.ToRoom(request.Room);
            var window = GeometryMapping.ToWindow(request.Window);
            var target = GeometryMapping.ToVector3(request.Target);
            var zone = TimeMapping.ToFixedZone(request.UtcOffsetHours);
            var startDate = new LocalDate(request.Year, request.Month, request.Day);

            var rawMatches = InverseAlignmentSolver.FindAlignments(
                room, window, target, request.Latitude, request.Longitude, zone, startDate, request.ToleranceDegrees);
            var topMatches = AlignmentMatchSummarizer.SummarizeTopMatches(rawMatches, request.MaxResults);

            var matches = topMatches.Select(match => new AlignmentMatchDto(
                match.DateTime.Year,
                match.DateTime.Month,
                match.DateTime.Day,
                match.DateTime.Hour,
                match.DateTime.Minute,
                match.SunPosition.ElevationDegrees,
                match.SunPosition.AzimuthDegrees,
                match.AngleDifferenceDegrees)).ToList();

            return Results.Ok(new AlignmentsResponse(matches));
        });
    }
}
