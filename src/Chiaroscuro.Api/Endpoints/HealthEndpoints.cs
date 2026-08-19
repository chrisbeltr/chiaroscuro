namespace Chiaroscuro.Api.Endpoints;

/// <summary>Polled by Electron's main process to know when the sidecar backend is ready.</summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
    }
}
