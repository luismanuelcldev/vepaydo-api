using System.Net;

namespace VepayDO.Application.Auth.Ports;

public sealed record PasswordResetTokenRecord
{
    public required Guid Id { get; init; }

    public required Guid UserId { get; init; }

    public DateTimeOffset? UsedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed record NewPasswordResetToken(
    Guid UserId,
    byte[] TokenHash,
    DateTimeOffset ExpiresAt,
    IPAddress? RequestedIp,
    Guid? IssuedBySupportId);

public interface IPasswordResetTokenRepository
{
    Task CreateAsync(NewPasswordResetToken token, CancellationToken cancellationToken = default);

    Task<PasswordResetTokenRecord?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    Task InvalidateUnusedForUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken = default);

    Task MarkUsedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default);
}
