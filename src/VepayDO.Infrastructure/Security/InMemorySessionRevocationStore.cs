using System.Collections.Concurrent;
using VepayDO.Application.Security;
using VepayDO.Domain.Time;

namespace VepayDO.Infrastructure.Security;

/// <summary>
/// Process-local session revocation list (AGENTS.md section 9.9). Entries live
/// for one access-token lifetime, which is long enough to make revocation take
/// effect on every token already issued and short enough to bound memory.
/// Replaced by Redis in M7 so a restart and a second process see the same list.
/// </summary>
public sealed class InMemorySessionRevocationStore(IClock clock) : ISessionRevocationStore
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _revoked = [];

    private long _lastSweepTicks;

    public Task<bool> IsRevokedAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_revoked.TryGetValue(sessionId, out var expiresAt))
        {
            return Task.FromResult(false);
        }

        if (expiresAt <= clock.UtcNow)
        {
            _revoked.TryRemove(sessionId, out _);
            return Task.FromResult(false);
        }

        SweepIfDue();
        return Task.FromResult(true);
    }

    public Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _revoked[sessionId] = clock.UtcNow + TokenConstants.AccessTokenLifetime;
        return Task.CompletedTask;
    }

    private void SweepIfDue()
    {
        var now = clock.UtcNow.UtcTicks;
        var last = Interlocked.Read(ref _lastSweepTicks);
        if (now - last < SweepInterval.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastSweepTicks, now, last) != last)
        {
            return;
        }

        var cutoff = clock.UtcNow;
        foreach (var entry in _revoked)
        {
            if (entry.Value <= cutoff)
            {
                _revoked.TryRemove(entry.Key, out _);
            }
        }
    }
}
