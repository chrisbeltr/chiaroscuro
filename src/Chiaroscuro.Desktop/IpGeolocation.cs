using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chiaroscuro.Desktop;

// Desktop has no built-in cross-platform GPS/location API, so "current location" falls back to
// an IP-based lookup - coarse (city-level), but works identically on Windows/Linux/macOS with no
// permission prompt. See MainViewModel.LocationProvider for how this gets wired in.
internal static class IpGeolocation
{
    private static readonly HttpClient Client = new();

    public static async Task<(double Latitude, double Longitude)?> GetCurrentLocationAsync()
    {
        try
        {
            using var response = await Client.GetAsync("http://ip-api.com/json/");
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(stream);

            var lat = json.RootElement.GetProperty("lat").GetDouble();
            var lon = json.RootElement.GetProperty("lon").GetDouble();
            return (lat, lon);
        }
        catch
        {
            // Network failure, malformed response, service down, etc. - the button just does
            // nothing rather than crashing the app; the user can still enter coordinates by hand.
            return null;
        }
    }
}
