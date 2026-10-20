using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Auth.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes <c>memberships</c> joined with <c>tenants</c> under the
/// request's tenant context, so RLS (<c>memberships_own</c>, <c>tenants_of_user</c>)
/// keeps the result limited to the caller's own memberships (AGENTS.md section 7).
/// </summary>
public sealed class MembershipRepository(TxProvider tx, IClock clock) : IMembershipRepository
{
    public async Task<IReadOnlyList<MembershipRecord>> ListActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var items = await (
                from membership in tx.DbContext.Memberships
                join tenant in tx.DbContext.Tenants on membership.TenantId equals tenant.Id
                where membership.UserId == userId && membership.IsActive
                orderby tenant.Name
                select new MembershipRecord
                {
                    TenantId = tenant.Id,
                    UserId = membership.UserId,
                    Role = membership.Role,
                    IsActive = membership.IsActive,
                    TenantName = tenant.Name,
                    CreatedAt = membership.CreatedAt,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items;
    }

    public async Task<MembershipRecord?> FindAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var item = await (
                from membership in tx.DbContext.Memberships
                join tenant in tx.DbContext.Tenants on membership.TenantId equals tenant.Id
                where membership.TenantId == tenantId && membership.UserId == userId
                select new MembershipRecord
                {
                    TenantId = tenant.Id,
                    UserId = membership.UserId,
                    Role = membership.Role,
                    IsActive = membership.IsActive,
                    TenantName = tenant.Name,
                    CreatedAt = membership.CreatedAt,
                })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return item;
    }

    public async Task<IReadOnlyList<MembershipRecord>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await (
                from membership in tx.DbContext.Memberships
                join tenant in tx.DbContext.Tenants on membership.TenantId equals tenant.Id
                join user in tx.DbContext.Users on membership.UserId equals user.Id
                where membership.TenantId == tenantId
                orderby user.Name
                select new MembershipRecord
                {
                    TenantId = tenant.Id,
                    UserId = membership.UserId,
                    Role = membership.Role,
                    IsActive = membership.IsActive,
                    TenantName = tenant.Name,
                    UserName = user.Name,
                    UserEmail = user.Email,
                    CreatedAt = membership.CreatedAt,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items;
    }

    public async Task<int> CountActiveOwnersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await tx.DbContext.Memberships
            .CountAsync(
                m => m.TenantId == tenantId && m.IsActive && m.Role == MemberRole.OWNER,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(
        Guid tenantId,
        Guid userId,
        MemberRole role,
        CancellationToken cancellationToken = default)
    {
        tx.DbContext.Memberships.Add(new Membership
        {
            TenantId = tenantId,
            UserId = userId,
            Role = role,
            IsActive = true,
            CreatedAt = clock.UtcNow,
        });

        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateRoleAsync(
        Guid tenantId,
        Guid userId,
        MemberRole role,
        CancellationToken cancellationToken = default) =>
        tx.DbContext.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.Role, role),
                cancellationToken);

    public Task SetActiveAsync(
        Guid tenantId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default) =>
        tx.DbContext.Memberships
            .Where(m => m.TenantId == tenantId && m.UserId == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(m => m.IsActive, isActive),
                cancellationToken);
}
