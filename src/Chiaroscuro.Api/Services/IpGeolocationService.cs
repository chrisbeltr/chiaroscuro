using System.Text.Json;

namespace Chiaroscuro.Api.Services;

// Ported near-verbatim from Chiaroscuro.Desktop/IpGeolocation.cs. Runs server-side (rather
// than in the browser/Electron renderer) so the free-tier, HTTP-only ip-api.com endpoint is
// never a mixed-content/CORS problem for callers - see GeolocationEndpoints for the route
// this backs. Only the Electron build calls this endpoint at all; the plain-web build
// resolves location via the browser's own navigator.geolocation instead.
public sealed class IpGeolocationService(HttpClient httpClient, ILogger<IpGeolocationService> logger) : IIpGeolocationService
{
    public async Task<(double Latitude, double Longitude, double UtcOffsetHours)?> GetCurrentLocationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // "fields=" opts into the "offset" field (UTC offset in seconds) - it's not in
            // ip-api.com's default field set, which otherwise only gives an IANA zone name.
            using var response = await httpClient.GetAsync("http://ip-api.com/json/?fields=status,lat,lon,offset", cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var lat = json.RootElement.GetProperty("lat").GetDouble();
            var lon = json.RootElement.GetProperty("lon").GetDouble();
            var utcOffsetHours = json.RootElement.GetProperty("offset").GetDouble() / 3600.0;
            return (lat, lon, utcOffsetHours);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Network failure, malformed response, service down, etc. - the endpoint just
            // reports failure rather than throwing; the caller falls back to manual entry.
            logger.LogWarning(ex, "IP geolocation lookup failed.");
            return null;
        }
    }
}
