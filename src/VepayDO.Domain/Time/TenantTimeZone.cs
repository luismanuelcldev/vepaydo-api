namespace VepayDO.Domain.Time;

public static class TenantTimeZone
{
    public const string Default = "America/Santo_Domingo";

    public static TimeZoneInfo Get(string? tenantTimeZone) =>
        TimeZoneInfo.FindSystemTimeZoneById(
            string.IsNullOrWhiteSpace(tenantTimeZone) ? Default : tenantTimeZone);
}
