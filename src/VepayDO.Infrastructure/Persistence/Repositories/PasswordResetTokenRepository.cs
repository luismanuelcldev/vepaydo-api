using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes the global <c>password_reset_tokens</c> table (no RLS). Only
/// the SHA-256 hash of the token is persisted, never the token itself
/// (AGENTS.md section 9.9).
/// </summary>
public sealed class PasswordResetTokenRepository(TxProvider tx, IClock clock) : IPasswordResetTokenRepository
{
    public async Task CreateAsync(
        NewPasswordResetToken token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        tx.DbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = token.UserId,
            TokenHash = token.TokenHash,
            ExpiresAt = token.ExpiresAt,
            RequestedIp = token.RequestedIp,
            IssuedBySupportId = token.IssuedBySupportId,
            CreatedAt = clock.UtcNow,
        });

        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PasswordResetTokenRecord?> FindByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken = default)
    {
        var token = await tx.DbContext.PasswordResetTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            return null;
        }

        return new PasswordResetTokenRecord
        {
            Id = token.Id,
            UserId = token.UserId,
            UsedAt = token.UsedAt,
            ExpiresAt = token.ExpiresAt,
        };
    }

    public Task InvalidateUnusedForUserAsync(
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        tx.DbContext.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.UsedAt, at),
                cancellationToken);

    public Task MarkUsedAsync(
        Guid id,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        tx.DbContext.PasswordResetTokens
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.UsedAt, at),
                cancellationToken);
}
