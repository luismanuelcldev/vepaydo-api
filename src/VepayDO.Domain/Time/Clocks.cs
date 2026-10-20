namespace VepayDO.Domain.Time;

/// <summary>Production clock. Registered as a DI singleton.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Deterministic clock for tests.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset fixedInstant)
    {
        UtcNow = fixedInstant;
    }

    public DateTimeOffset UtcNow { get; }

    public static FixedClock At(string isoInstant) => new(DateTimeOffset.Parse(
        isoInstant,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal));
}
