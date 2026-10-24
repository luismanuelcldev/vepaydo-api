using Microsoft.EntityFrameworkCore;

using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Time;
using VepayDO.Domain.Tenants;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class TenantSettingsRepository(TxProvider tx, IClock clock) : ITenantSettingsRepository
{
    public async Task<TenantSettingsValues?> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var row = await tx.DbContext.TenantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? null
            : new TenantSettingsValues
            {
                DateWindowDays = row.DateWindowDays,
                AmountTolerance = row.AmountTolerance,
                VerifierAmountCap = row.VerifierAmountCap,
                ImageRetentionMonths = row.ImageRetentionMonths,
            };
    }

    public async Task UpdateAsync(
        Guid tenantId,
        TenantSettingsValues values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var affected = await tx.DbContext.TenantSettings
            .Where(s => s.TenantId == tenantId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.DateWindowDays, values.DateWindowDays)
                    .SetProperty(s => s.AmountTolerance, values.AmountTolerance)
                    .SetProperty(s => s.VerifierAmountCap, values.VerifierAmountCap)
                    .SetProperty(s => s.ImageRetentionMonths, values.ImageRetentionMonths)
                    .SetProperty(s => s.UpdatedAt, clock.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
        {
            tx.DbContext.TenantSettings.Add(new TenantSettings
            {
                TenantId = tenantId,
                DateWindowDays = values.DateWindowDays,
                AmountTolerance = values.AmountTolerance,
                VerifierAmountCap = values.VerifierAmountCap,
                ImageRetentionMonths = values.ImageRetentionMonths,
                UpdatedAt = clock.UtcNow,
            });

            await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
