using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;

namespace Chiaroscuro.Wasm;

// Wraps the browser's navigator.geolocation (asks the user for permission the first time) via
// the chiaroscuroGetLocation() glue function defined in wwwroot/main.js. Returns a JSON string
// rather than a marshaled array/object - JSImport's built-in marshaling for numeric arrays needs
// extra JSType attributes, whereas a string round-trip through System.Text.Json is simpler and
// just as reliable for two doubles.
[SupportedOSPlatform("browser")]
internal static partial class BrowserGeolocation
{
    [JSImport("globalThis.chiaroscuroGetLocation")]
    private static partial Task<string> GetLocationJsonAsync();

    public static async Task<(double Latitude, double Longitude)?> GetCurrentLocationAsync()
    {
        try
        {
            var json = await GetLocationJsonAsync();
            using var doc = JsonDocument.Parse(json);
            var lat = doc.RootElement.GetProperty("lat").GetDouble();
            var lon = doc.RootElement.GetProperty("lon").GetDouble();
            return (lat, lon);
        }
        catch
        {
            // Permission denied, geolocation unsupported, user dismissed the prompt, etc. - the
            // button just does nothing rather than crashing the app.
            return null;
        }
    }
}
