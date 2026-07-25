import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// Called from C# via [JSImport("globalThis.chiaroscuroGetLocation")] in BrowserGeolocation.cs.
// Wraps navigator.geolocation's callback-based API in a Promise (which JSImport can await as a
// Task<string>), and returns JSON rather than the raw GeolocationPosition object since JSImport's
// object marshaling would need extra type mapping for no real benefit here.
globalThis.chiaroscuroGetLocation = function () {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(new Error("Geolocation is not supported by this browser"));
            return;
        }
        navigator.geolocation.getCurrentPosition(
            pos => resolve(JSON.stringify({ lat: pos.coords.latitude, lon: pos.coords.longitude })),
            err => reject(err));
    });
};

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

// StartBrowserAppAsync's Task resolves once Avalonia has mounted its canvas into #out, not when
// the app exits (there's no blocking "run loop" in the browser - Avalonia keeps rendering via
// callbacks after this point) - so this is the right place to remove the loading splash. It has
// to be removed explicitly rather than just left behind: it's position:absolute, which always
// paints above the plain (non-positioned) canvas Avalonia inserts, so it would otherwise cover
// the app permanently.
document.querySelector(".chiaroscuro-splash")?.remove();
