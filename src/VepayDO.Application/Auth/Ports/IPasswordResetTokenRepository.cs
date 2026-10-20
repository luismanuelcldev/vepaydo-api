using System.Net;

namespace VepayDO.Application.Auth.Ports;

/// <summary>A password-reset token row (hash only) as the application layer needs it.</summary>
public sealed record PasswordResetTokenRecord
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public DateTimeOffset? UsedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Data required to create a password-reset token.</summary>
public sealed record NewPasswordResetToken(
    Guid UserId,
    byte[] TokenHash,
    DateTimeOffset ExpiresAt,
    IPAddress? RequestedIp,
    Guid? IssuedBySupportId);

/// <summary>
/// Reads and writes <c>password_reset_tokens</c> (global, no RLS). Only the
/// SHA-256 hash is stored, never the token itself (AGENTS.md section 9.9).
/// </summary>
public interface IPasswordResetTokenRepository
{
    Task CreateAsync(NewPasswordResetToken token, CancellationToken cancellationToken = default);

    Task<PasswordResetTokenRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    Task InvalidateUnusedForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task MarkUsedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default);
}
