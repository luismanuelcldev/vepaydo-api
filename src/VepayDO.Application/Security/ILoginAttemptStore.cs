namespace VepayDO.Application.Security;

/// <summary>
/// Login rate limiting and lockout backoff (AGENTS.md section 13). Keys are
/// deliberately caller-defined (email, IP, or both) so the policy that composes
/// them stays in one place. In-memory for now; Redis in M7.
/// </summary>
public interface ILoginAttemptStore
{
    /// <summary>True when the key is currently locked out.</summary>
    Task<bool> IsBlockedAsync(string key, CancellationToken cancellationToken = default);

    Task RegisterFailureAsync(string key, CancellationToken cancellationToken = default);

    Task ResetAsync(string key, CancellationToken cancellationToken = default);
}
