using System.Net;

namespace VepayDO.Application.Auth.Ports;

/// <summary>A refresh-token session row as the application layer needs it.</summary>
public sealed record SessionRecord
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }

    public string? DeviceLabel { get; init; }

    public IPAddress? Ip { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }
}

/// <summary>Data required to create a session.</summary>
public sealed record NewSession(
    Guid UserId,
    Guid? TenantId,
    byte[] TokenHash,
    DateTimeOffset ExpiresAt,
    string? UserAgent,
    string? DeviceLabel,
    IPAddress? Ip,
    DateTimeOffset CreatedAt);

/// <summary>
/// Reads and writes <c>sessions</c>. The table is global (no RLS); only the
/// SHA-256 hash of the refresh token is ever stored (AGENTS.md section 13).
/// </summary>
public interface ISessionRepository
{
    Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default);

    Task<SessionRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    Task<SessionRecord?> FindActiveByTokenHashAsync(
        byte[] tokenHash,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionRecord>> ListActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<bool> RevokeOwnedAsync(Guid sessionId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session of the user and returns the affected ids.</summary>
    Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session of the user except one and returns the affected ids.</summary>
    Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(Guid userId, Guid keepSessionId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task BindTenantAsync(byte[] tokenHash, Guid userId, Guid tenantId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default);
}
