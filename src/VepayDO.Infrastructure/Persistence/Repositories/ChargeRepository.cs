using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed partial class ChargeRepository(TxProvider tx, IClock clock) : IChargeRepository
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
