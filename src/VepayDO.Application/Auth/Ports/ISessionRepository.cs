using System.Net;

namespace VepayDO.Application.Auth.Ports;

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

public sealed record NewSession(
    Guid UserId,
    Guid? TenantId,
    byte[] TokenHash,
    DateTimeOffset ExpiresAt,
    string? UserAgent,
    string? DeviceLabel,
    IPAddress? Ip,
    DateTimeOffset CreatedAt);

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

    Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(Guid userId, Guid keepSessionId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task BindTenantAsync(byte[] tokenHash, Guid userId, Guid tenantId, DateTimeOffset lastSeenAt, CancellationToken cancellationToken = default);
}
