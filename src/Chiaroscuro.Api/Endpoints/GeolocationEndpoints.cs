using Chiaroscuro.Api.Contracts;
using Chiaroscuro.Api.Services;

namespace Chiaroscuro.Api.Endpoints;

public static class GeolocationEndpoints
{
    public static void MapGeolocationEndpoints(this IEndpointRouteBuilder app)
    {
        // Electron-only: the plain-web build resolves location via the browser's own
        // navigator.geolocation and never calls this endpoint.
        app.MapGet("/api/geolocation/ip", async (IIpGeolocationService geolocation, CancellationToken cancellationToken) =>
        {
            var location = await geolocation.GetCurrentLocationAsync(cancellationToken);

            return location is { } current
                ? Results.Ok(new GeolocationResponse(true, current.Latitude, current.Longitude, current.UtcOffsetHours))
                : Results.Ok(new GeolocationResponse(false, null, null, null));
        });
    }
}
