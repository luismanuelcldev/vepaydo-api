using Microsoft.EntityFrameworkCore;

using Npgsql;

using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads and writes the current tenant's <c>charges</c> rows under the request's
/// tenant context. The table carries the simple <c>tenant_isolation</c> RLS policy,
/// so every query is confined to <c>app.tenant_id</c>. Reads go through
/// <c>v_charge_balances</c> (a security-invoker view), which is the complete read
/// model: it aggregates the active allocations per charge and carries every
/// column the API needs, so a read is a single query (AGENTS.md sections 7, 9.2).
/// </summary>
public sealed class ChargeRepository(TxProvider tx, IClock clock) : IChargeRepository
{
    public async Task<IReadOnlyList<ChargeRecord>> ListAsync(
        Guid tenantId,
        ChargeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var balancesQuery = tx.DbContext.ChargeBalances
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId);

        if (query.Status is not null && Enum.TryParse<ChargeStatus>(query.Status, ignoreCase: true, out var status))
        {
            balancesQuery = balancesQuery.Where(b => b.Status == status);
        }

        if (query.CustomerId is not null)
        {
            balancesQuery = balancesQuery.Where(b => b.CustomerId == query.CustomerId);
        }

        var rows = await balancesQuery
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.ChargeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<ChargeRecord?> FindAsync(
        Guid tenantId,
        Guid chargeId,
        CancellationToken cancellationToken = default)
    {
        var row = await tx.DbContext.ChargeBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.ChargeId == chargeId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToRecord(row);
    }

    public async Task<bool> CodeExistsAsync(
        Guid tenantId,
        string code,
        Guid? excludingChargeId,
        CancellationToken cancellationToken = default)
    {
        return await tx.DbContext.Charges
            .AsNoTracking()
            .AnyAsync(
                c => c.TenantId == tenantId
                     && c.Code == code
                     && (excludingChargeId == null || c.Id != excludingChargeId.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Guid> AddAsync(
        Guid tenantId,
        NewCharge charge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(charge);

        var entity = Build(tenantId, charge);
        tx.DbContext.Charges.Add(entity);

        try
        {
            await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The (tenant_id, code) unique index is the backstop for a concurrent
            // insert that won the race after the service's existence check. The
            // service turns this into a retry with a freshly generated code.
            throw new ConflictException("Charge code already exists in this tenant");
        }

        return entity.Id;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid chargeId,
        UpdateChargeFields fields,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fields);

        await tx.DbContext.Charges
            .Where(c => c.TenantId == tenantId && c.Id == chargeId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.CustomerId, fields.CustomerId)
                    .SetProperty(c => c.Code, fields.Code)
                    .SetProperty(c => c.Description, fields.Description)
                    .SetProperty(c => c.Amount, fields.Amount)
                    .SetProperty(c => c.Currency, fields.Currency)
                    .SetProperty(c => c.IssuedOn, fields.IssuedOn)
                    .SetProperty(c => c.DueOn, fields.DueOn)
                    .SetProperty(c => c.UpdatedAt, clock.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task VoidAsync(
        Guid tenantId,
        Guid chargeId,
        string reason,
        Guid? voidedBy,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        await tx.DbContext.Charges
            .Where(c => c.TenantId == tenantId && c.Id == chargeId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.Status, ChargeStatus.VOIDED)
                    .SetProperty(c => c.VoidedAt, now)
                    .SetProperty(c => c.VoidedBy, voidedBy)
                    .SetProperty(c => c.VoidReason, reason)
                    .SetProperty(c => c.UpdatedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddManyAsync(
        Guid tenantId,
        IReadOnlyList<NewCharge> charges,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(charges);

        var entities = charges.Select(c => Build(tenantId, c)).ToList();
        tx.DbContext.Charges.AddRange(entities);

        try
        {
            await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException("Charge code already exists in this tenant");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private Charge Build(Guid tenantId, NewCharge charge)
    {
        var now = clock.UtcNow;
        return new Charge
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = charge.CustomerId,
            Code = charge.Code,
            Description = charge.Description,
            Amount = charge.Amount,
            Currency = charge.Currency,
            IssuedOn = charge.IssuedOn,
            DueOn = charge.DueOn,
            Status = ChargeStatus.OPEN,
            CreatedBy = charge.CreatedBy,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static ChargeRecord ToRecord(ChargeBalanceRow row) =>
        new(
            row.ChargeId,
            row.CustomerId,
            row.Code,
            row.Description,
            row.Amount,
            row.Currency,
            row.IssuedOn,
            row.DueOn,
            row.Status.ToString(),
            row.AmountApplied,
            row.Balance,
            row.VoidedAt,
            row.VoidReason,
            row.CreatedAt,
            row.UpdatedAt);
}
