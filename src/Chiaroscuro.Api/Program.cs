using System.Text.Json.Serialization;
using Chiaroscuro.Api.Endpoints;
using Chiaroscuro.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// WallOrientation/RoomSurface cross the wire as strings (e.g. "North"), not raw ints.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHttpClient<IIpGeolocationService, IpGeolocationService>();

// The API never serves the frontend itself - the SPA is always a separately deployed
// artifact (bundled in Electron, or a standalone static site for hosted-web), so CORS is
// required in every deployment mode, not just dev. AllowedOrigins is set per-environment:
// see appsettings.Development.json for the local Vite dev server entry.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapHealthEndpoints();
app.MapSolarEndpoints();
app.MapGeolocationEndpoints();

app.Run();

// Exposes the generated entry point type to Chiaroscuro.Api.Tests' WebApplicationFactory<Program>.
public partial class Program;
