using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class MembershipRepository(TxProvider tx, IClock clock) : IMembershipRepository
{
    public async Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await (from m in tx.DbContext.Memberships join t in tx.DbContext.Tenants on m.TenantId equals t.Id
                      where m.UserId == userId && m.IsActive orderby t.Name
                      select new MembershipRecord { TenantId = t.Id, UserId = m.UserId, Role = m.Role, IsActive = m.IsActive, TenantName = t.Name, CreatedAt = m.CreatedAt })
            .AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<MembershipRecord?> FindAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await (from m in tx.DbContext.Memberships join t in tx.DbContext.Tenants on m.TenantId equals t.Id
                      where m.TenantId == tenantId && m.UserId == userId
                      select new MembershipRecord { TenantId = t.Id, UserId = m.UserId, Role = m.Role, IsActive = m.IsActive, TenantName = t.Name, CreatedAt = m.CreatedAt })
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await (from m in tx.DbContext.Memberships join t in tx.DbContext.Tenants on m.TenantId equals t.Id join u in tx.DbContext.Users on m.UserId equals u.Id
                      where m.TenantId == tenantId orderby u.Name
                      select new MembershipRecord { TenantId = t.Id, UserId = m.UserId, Role = m.Role, IsActive = m.IsActive, TenantName = t.Name, UserName = u.Name, UserEmail = u.Email, CreatedAt = m.CreatedAt })
            .AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountActiveOwnersAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await tx.DbContext.Memberships.CountAsync(m => m.TenantId == tenantId && m.IsActive && m.Role == MemberRole.OWNER, cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default)
    {
        tx.DbContext.Memberships.Add(new Membership { TenantId = tenantId, UserId = userId, Role = role, IsActive = true, CreatedAt = clock.UtcNow });
        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateRoleAsync(Guid tenantId, Guid userId, MemberRole role, CancellationToken cancellationToken = default) =>
        tx.DbContext.Memberships.Where(m => m.TenantId == tenantId && m.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(m => m.Role, role), cancellationToken);

    public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
        tx.DbContext.Memberships.Where(m => m.TenantId == tenantId && m.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsActive, isActive), cancellationToken);
}
