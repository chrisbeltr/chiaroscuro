using System.Net.Http.Json;
using Chiaroscuro.Api.Contracts;
using Chiaroscuro.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chiaroscuro.Api.Tests;

public class GeolocationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task IpGeolocation_WhenLookupSucceeds_ReturnsCoordinates()
    {
        using var client = WithGeolocationService(new FakeIpGeolocationService((40.7128, -74.0060, -5.0))).CreateClient();

        var body = await client.GetFromJsonAsync<GeolocationResponse>("/api/geolocation/ip");

        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Equal(40.7128, body.Latitude);
        Assert.Equal(-74.0060, body.Longitude);
        Assert.Equal(-5.0, body.UtcOffsetHours);
    }

    [Fact]
    public async Task IpGeolocation_WhenLookupFails_ReturnsUnsuccessfulWithNoCoordinates()
    {
        using var client = WithGeolocationService(new FakeIpGeolocationService(null)).CreateClient();

        var body = await client.GetFromJsonAsync<GeolocationResponse>("/api/geolocation/ip");

        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Null(body.Latitude);
        Assert.Null(body.Longitude);
        Assert.Null(body.UtcOffsetHours);
    }

    private WebApplicationFactoryWrapper WithGeolocationService(IIpGeolocationService fake) => new(factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIpGeolocationService>();
            services.AddSingleton(fake);
        })));

    // Thin wrapper so call sites read as "give me a client configured with this fake" without
    // exposing WebApplicationFactory<Program>'s broader surface area to every test.
    private sealed class WebApplicationFactoryWrapper(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> inner)
    {
        public HttpClient CreateClient() => inner.CreateClient();
    }

    private sealed class FakeIpGeolocationService((double, double, double)? result) : IIpGeolocationService
    {
        public Task<(double Latitude, double Longitude, double UtcOffsetHours)?> GetCurrentLocationAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
