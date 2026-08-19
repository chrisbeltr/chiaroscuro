namespace Chiaroscuro.Api.Services;

public interface IIpGeolocationService
{
    Task<(double Latitude, double Longitude, double UtcOffsetHours)?> GetCurrentLocationAsync(CancellationToken cancellationToken = default);
}
