using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chiaroscuro.Api.Contracts;
using Chiaroscuro.Core.Geometry;
using Chiaroscuro.Core.InverseSolver;
using Chiaroscuro.Core.Solar;
using NodaTime;

namespace Chiaroscuro.Api.Tests;

/// <summary>
/// Drives /api/solar/illuminate and /api/solar/alignments with the same fixture inputs
/// Chiaroscuro.Core.Tests' RayTracerTests/InverseAlignmentSolverTests already use, then
/// compares the HTTP response against calling Chiaroscuro.Core directly - confirming the
/// DTO mapping layer (GeometryMapping/TimeMapping) is lossless, not re-testing the math itself.
/// </summary>
public class SolarEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Matches ASP.NET Core's default HTTP JSON options (camelCase property names) so
        // responses deserialize into these PascalCase-named DTO properties correctly.
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Illuminate_SunBelowHorizon_ReturnsNullIllumination()
    {
        // Same fixture as RayTracerTests.Trace_ReturnsNull_WhenLightTravelsAwayFromTheRoomInterior,
        // driven via a date/time+location that Chiaroscuro.Core's SolarCalculator resolves to
        // that same below-horizon sun position isn't practical here, so this test instead
        // sends a request built to produce a null illumination and asserts the shape.
        using var client = factory.CreateClient();
        var request = new IlluminationRequest(
            Room: new RoomDto(Width: 5, Length: 4, Height: 3),
            Window: new WindowDto(WallOrientation.North, HorizontalOffset: 0, SillHeight: 1, Width: 1, Height: 1),
            Latitude: 40.7128,
            Longitude: -74.0060,
            Year: 2026, Month: 6, Day: 21, Hour: 4, Minute: 0, // pre-dawn UTC for this longitude
            UtcOffsetHours: 0);

        using var response = await client.PostAsJsonAsync("/api/solar/illuminate", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IlluminationResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Null(body.Illumination);
    }

    [Fact]
    public async Task Illuminate_MatchesDirectCoreCall_ForAKnownSouthWindowScenario()
    {
        // Same fixture as InverseAlignmentSolverTests' shared scenario: a South-facing
        // window, known to illuminate the room at 2026-01-15 17:00 UTC.
        var room = new Room(Width: 6, Length: 5, Height: 3);
        var window = new Window(WallOrientation.South, HorizontalOffset: 0, SillHeight: 1, Width: 1.2, Height: 1.5);
        var dateTime = new LocalDateTime(2026, 1, 15, 17, 0, 0).InUtc();
        var expectedSun = SolarCalculator.Calculate(40.7128, -74.0060, dateTime);
        var expectedTrace = RayTracer.Trace(room, window, expectedSun);
        Assert.NotNull(expectedTrace); // sanity check on the fixture itself

        using var client = factory.CreateClient();
        var request = new IlluminationRequest(
            Room: new RoomDto(room.Width, room.Length, room.Height, room.RotationDegrees),
            Window: new WindowDto(window.Wall, window.HorizontalOffset, window.SillHeight, window.Width, window.Height),
            Latitude: 40.7128,
            Longitude: -74.0060,
            Year: 2026, Month: 1, Day: 15, Hour: 17, Minute: 0,
            UtcOffsetHours: 0);

        using var response = await client.PostAsJsonAsync("/api/solar/illuminate", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IlluminationResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(expectedSun.ElevationDegrees, body.SunPosition.ElevationDegrees, precision: 6);
        Assert.Equal(expectedSun.AzimuthDegrees, body.SunPosition.AzimuthDegrees, precision: 6);
        Assert.NotNull(body.Illumination);
        Assert.Equal(expectedTrace!.Value.Surface, body.Illumination.Surface);
        Assert.Equal(expectedTrace.Value.CenterPoint.X, body.Illumination.CenterPoint.X, precision: 6);
        Assert.Equal(expectedTrace.Value.CenterPoint.Y, body.Illumination.CenterPoint.Y, precision: 6);
        Assert.Equal(expectedTrace.Value.CenterPoint.Z, body.Illumination.CenterPoint.Z, precision: 6);
    }

    [Fact]
    public async Task Alignments_MatchesDirectCoreCall_ForTheKnownRoundTripScenario()
    {
        var room = new Room(Width: 6, Length: 5, Height: 3);
        var window = new Window(WallOrientation.South, HorizontalOffset: 0, SillHeight: 1, Width: 1.2, Height: 1.5);
        var knownDateTime = new LocalDateTime(2026, 1, 15, 17, 0, 0).InZoneStrictly(DateTimeZone.Utc);
        var knownSun = SolarCalculator.Calculate(40.7128, -74.0060, knownDateTime);
        var traceResult = RayTracer.Trace(room, window, knownSun);
        Assert.NotNull(traceResult);
        var targetPoint = traceResult!.Value.CenterPoint;

        var expectedMatches = InverseAlignmentSolver.FindAlignments(
            room, window, targetPoint, 40.7128, -74.0060, DateTimeZone.Utc, new LocalDate(2026, 1, 1), toleranceDegrees: 0.1);
        var expectedTop = AlignmentMatchSummarizer.SummarizeTopMatches(expectedMatches, maxResults: 15);

        using var client = factory.CreateClient();
        var request = new AlignmentsRequest(
            Room: new RoomDto(room.Width, room.Length, room.Height, room.RotationDegrees),
            Window: new WindowDto(window.Wall, window.HorizontalOffset, window.SillHeight, window.Width, window.Height),
            Target: new Vector3Dto(targetPoint.X, targetPoint.Y, targetPoint.Z),
            Latitude: 40.7128,
            Longitude: -74.0060,
            UtcOffsetHours: 0,
            Year: 2026, Month: 1, Day: 1,
            ToleranceDegrees: 0.1);

        using var response = await client.PostAsJsonAsync("/api/solar/alignments", request, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AlignmentsResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(expectedTop.Count, body.Matches.Count);
        Assert.Contains(body.Matches, match =>
            match is { Year: 2026, Month: 1, Day: 15, Hour: 17, Minute: 0 });
    }
}
