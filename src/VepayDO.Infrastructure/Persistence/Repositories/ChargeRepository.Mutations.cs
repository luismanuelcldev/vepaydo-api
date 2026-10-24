using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence.Entities;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed partial class ChargeRepository
{
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
}
