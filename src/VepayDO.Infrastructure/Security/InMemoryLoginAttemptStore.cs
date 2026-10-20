using System.Collections.Concurrent;
using VepayDO.Application.Security;
using VepayDO.Domain.Time;

namespace VepayDO.Infrastructure.Security;

/// <summary>
/// Login rate limiting with a fixed lockout window (AGENTS.md section 13).
/// Five failures inside fifteen minutes block the key until the window elapses;
/// a successful login clears it. Constants live here on purpose so the policy is
/// one reviewable place while the pilot data tells us where to tune it.
/// Replaced by Redis in M7 so the limit holds across processes.
/// </summary>
public sealed class InMemoryLoginAttemptStore(IClock clock) : ILoginAttemptStore
{
    public const int MaxAttempts = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Attempt> _attempts = new(StringComparer.Ordinal);

    public Task<bool> IsBlockedAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_attempts.TryGetValue(key, out var attempt))
        {
            return Task.FromResult(false);
        }

        if (attempt.WindowStart + Window <= clock.UtcNow)
        {
            _attempts.TryRemove(key, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(attempt.Failures >= MaxAttempts);
    }

    public Task RegisterFailureAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        cancellationToken.ThrowIfCancellationRequested();

        _attempts.AddOrUpdate(
            key,
            _ => new Attempt(1, clock.UtcNow),
            (_, current) => current.WindowStart + Window <= clock.UtcNow
                ? new Attempt(1, clock.UtcNow)
                : new Attempt(current.Failures + 1, current.WindowStart));

        return Task.CompletedTask;
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        cancellationToken.ThrowIfCancellationRequested();

        _attempts.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private readonly record struct Attempt(int Failures, DateTimeOffset WindowStart);
}
