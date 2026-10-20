using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes the global <c>sessions</c> table (no RLS). Only the refresh
/// token hash ever reaches this table (AGENTS.md section 13).
/// </summary>
public sealed class SessionRepository(TxProvider tx) : ISessionRepository
{
    public async Task<Guid> CreateAsync(NewSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var id = Guid.NewGuid();

        tx.DbContext.Sessions.Add(new Session
        {
            Id = id,
            UserId = session.UserId,
            TenantId = session.TenantId,
            TokenHash = session.TokenHash,
            ExpiresAt = session.ExpiresAt,
            UserAgent = session.UserAgent,
            DeviceLabel = session.DeviceLabel,
            Ip = session.Ip,
            CreatedAt = session.CreatedAt,
        });

        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<SessionRecord?> FindByTokenHashAsync(
        byte[] tokenHash,
        CancellationToken cancellationToken = default)
    {
        var session = await Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        return session is null ? null : ToRecord(session);
    }

    public async Task<SessionRecord?> FindActiveByTokenHashAsync(
        byte[] tokenHash,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var session = await Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TokenHash == tokenHash && s.UserId == userId && s.RevokedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        return session is null ? null : ToRecord(session);
    }

    public async Task<IReadOnlyList<SessionRecord>> ListActiveAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var sessions = await Query()
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sessions.ConvertAll(ToRecord);
    }

    public async Task<bool> RevokeOwnedAsync(
        Guid sessionId,
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var affected = await Query()
            .Where(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.RevokedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

        return affected > 0;
    }

    public Task<IReadOnlyList<Guid>> RevokeAllForUserAsync(
        Guid userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        RevokeManyAsync(
            Query().Where(s => s.UserId == userId && s.RevokedAt == null),
            at,
            cancellationToken);

    public Task<IReadOnlyList<Guid>> RevokeAllExceptAsync(
        Guid userId,
        Guid keepSessionId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        RevokeManyAsync(
            Query().Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != keepSessionId),
            at,
            cancellationToken);

    public async Task BindTenantAsync(
        byte[] tokenHash,
        Guid userId,
        Guid tenantId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        await Query()
            .Where(s => s.TokenHash == tokenHash && s.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.TenantId, tenantId)
                    .SetProperty(s => s.LastSeenAt, lastSeenAt),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private DbSet<Session> Query() => tx.DbContext.Sessions;

    private async Task<IReadOnlyList<Guid>> RevokeManyAsync(
        IQueryable<Session> query,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var ids = await query
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ids.Count == 0)
        {
            return ids;
        }

        await tx.DbContext.Sessions
            .Where(s => ids.Contains(s.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.RevokedAt, at),
                cancellationToken)
            .ConfigureAwait(false);

        return ids;
    }

    private static SessionRecord ToRecord(Session session) => new()
    {
        Id = session.Id,
        UserId = session.UserId,
        TenantId = session.TenantId,
        ExpiresAt = session.ExpiresAt,
        RevokedAt = session.RevokedAt,
        DeviceLabel = session.DeviceLabel,
        Ip = session.Ip,
        CreatedAt = session.CreatedAt,
        LastSeenAt = session.LastSeenAt,
    };
}
