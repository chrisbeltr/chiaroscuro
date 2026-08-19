namespace Chiaroscuro.Api.Contracts;

public sealed record IlluminationResponse(SunPositionDto SunPosition, IlluminationDto? Illumination);
