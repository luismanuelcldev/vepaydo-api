namespace VepayDO.Application.Security;

/// <summary>
/// Immediate rejection of access tokens whose session has been revoked. The
/// access JWT lives 15 minutes, so revocation is pushed to this store with a
/// TTL equal to the token lifetime instead of hitting the database on every
/// request (AGENTS.md section 9.9). In-memory for now; Redis in M7.
/// </summary>
public interface ISessionRevocationStore
{
    Task<bool> IsRevokedAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a session for at least one access-token lifetime, so every token
    /// already issued for it becomes unusable immediately.
    /// </summary>
    Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
