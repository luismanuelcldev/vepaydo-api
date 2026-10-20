namespace VepayDO.Domain.Time;

public static class ClockExtensions
{
    /// <summary>Current local date in the given tenant time zone.</summary>
    public static DateOnly Today(this IClock clock, TimeZoneInfo tenantTimeZone)
    {
        ArgumentNullException.ThrowIfNull(tenantTimeZone);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(clock.UtcNow, tenantTimeZone);
        return DateOnly.FromDateTime(local.Date);
    }
}
