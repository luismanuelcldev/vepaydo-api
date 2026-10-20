namespace VepayDO.Domain.Time;

/// <summary>
/// Abstraction over the current time. Domain code never calls
/// <c>DateTimeOffset.UtcNow</c> directly; it depends on this port so behavior is
/// deterministic in tests (AGENTS.md section 3.3). The clock is a DI singleton.
/// </summary>
public interface IClock
{
    /// <summary>Current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
