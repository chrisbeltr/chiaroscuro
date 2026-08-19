using NodaTime;

namespace Chiaroscuro.Api.Mapping;

/// <summary>
/// Builds NodaTime instants/zones from the plain Y/M/D/H/M + UTC-offset primitives DTOs
/// carry over the wire, exactly the way MainViewModel.Recalculate() builds them from its
/// own raw input fields - NodaTime types never cross the API boundary themselves.
/// </summary>
public static class TimeMapping
{
    public static Instant ToInstant(int year, int month, int day, int hour, int minute, double utcOffsetHours)
    {
        var localDateTime = new LocalDate(year, month, day).At(new LocalTime(hour, minute));
        var offset = Offset.FromTimeSpan(TimeSpan.FromHours(utcOffsetHours));
        return new OffsetDateTime(localDateTime, offset).ToInstant();
    }

    /// <summary>A fixed-offset (non-DST) zone for the given UTC offset, matching how
    /// MainViewModel derives a DateTimeZone from its raw UtcOffsetHours field.</summary>
    public static DateTimeZone ToFixedZone(double utcOffsetHours) =>
        DateTimeZone.ForOffset(Offset.FromTimeSpan(TimeSpan.FromHours(utcOffsetHours)));
}
