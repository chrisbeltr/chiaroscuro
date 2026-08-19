using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Chiaroscuro.Api.Tests;

/// <summary>Boots the real Chiaroscuro.Api pipeline in-process, with a fixed AllowedOrigins
/// entry so CORS behavior is exercised the same way it would be against the Vite dev server.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("AllowedOrigins:0", "http://localhost:5173");
    }
}
