using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Chiaroscuro.UI;
using Chiaroscuro.UI.ViewModels;
using Chiaroscuro.Wasm;

internal sealed class Program
{
    // Chiaroscuro.UI.Program's BuildAvaloniaApp() calls UsePlatformDetect(), which only exists
    // via the Avalonia.Desktop package (deliberately hidden from this project, see
    // Chiaroscuro.UI.csproj's PrivateAssets="All") and wouldn't select the browser backend
    // anyway - so the browser head configures its own AppBuilder instead.
    private static Task Main(string[] args)
    {
        MainViewModel.LocationProvider = BrowserGeolocation.GetCurrentLocationAsync;
        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
}
