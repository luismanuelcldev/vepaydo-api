using FluentAssertions;
using VepayDO.Domain.Time;

namespace VepayDO.Domain.Tests;

public sealed class ClockTests
{
    [Fact]
    public void System_clock_returns_utc_now()
    {
        var clock = new SystemClock();

        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero);
        clock.UtcNow.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Fixed_clock_returns_the_fixed_instant()
    {
        var clock = FixedClock.At("2026-01-15T12:00:00Z");

        clock.UtcNow.Should().Be(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Today_uses_the_tenant_time_zone()
    {

        var clock = FixedClock.At("2026-01-15T03:00:00Z");
        TimeZoneInfo zone = TenantTimeZone.Get(TenantTimeZone.Default);

        clock.Today(zone).Should().Be(new DateOnly(2026, 1, 14));
    }

    [Fact]
    public void Default_tenant_time_zone_is_santo_domingo()
    {
        TenantTimeZone.Default.Should().Be("America/Santo_Domingo");
    }
}
