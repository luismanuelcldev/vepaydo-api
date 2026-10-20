namespace VepayDO.Domain.Time;

/// <summary>
/// The default tenant time zone. All date logic (business days, "today", date
/// windows) is evaluated in this zone unless a tenant overrides it (AGENTS.md
/// section 3.3).
/// </summary>
public static class TenantTimeZone
{
    public const string Default = "America/Santo_Domingo";

    public static TimeZoneInfo Get(string? tenantTimeZone) =>
        TimeZoneInfo.FindSystemTimeZoneById(
            string.IsNullOrWhiteSpace(tenantTimeZone) ? Default : tenantTimeZone);
}
