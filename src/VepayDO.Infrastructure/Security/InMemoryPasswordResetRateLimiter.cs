using VepayDO.Application.Auth;
using VepayDO.Application.Auth.Ports;
using VepayDO.Domain.Time;

namespace VepayDO.Infrastructure.Security;

public sealed class InMemoryPasswordResetRateLimiter(IClock clock) : IPasswordResetRateLimiter
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _emailHits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DateTimeOffset>> _ipHits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastIssued = new(StringComparer.Ordinal);

    public bool TryConsumeEmail(string email) =>
        Consume(_emailHits, email, AuthConstants.ForgotPasswordMaxPerEmail);

    public bool TryConsumeIp(string ipAddress) =>
        Consume(_ipHits, ipAddress, AuthConstants.ForgotPasswordMaxPerIp);

    public bool IsWithinCooldown(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        lock (_gate)
        {
            return _lastIssued.TryGetValue(email, out var issuedAt)
                   && issuedAt + AuthConstants.ForgotPasswordResendCooldown > clock.UtcNow;
        }
    }

    public void MarkIssued(string email)
    {
        ArgumentNullException.ThrowIfNull(email);

        lock (_gate)
        {
            _lastIssued[email] = clock.UtcNow;
        }
    }

    private bool Consume(Dictionary<string, List<DateTimeOffset>> hits, string key, int max)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = clock.UtcNow;
        var windowStart = now - AuthConstants.ForgotPasswordWindow;

        lock (_gate)
        {
            if (!hits.TryGetValue(key, out var timestamps))
            {
                timestamps = [];
                hits[key] = timestamps;
            }

            timestamps.RemoveAll(at => at <= windowStart);

            if (timestamps.Count >= max)
            {
                return false;
            }

            timestamps.Add(now);
            return true;
        }
    }
}
