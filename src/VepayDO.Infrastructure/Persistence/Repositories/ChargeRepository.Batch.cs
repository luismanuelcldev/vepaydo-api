using Microsoft.EntityFrameworkCore;
using Npgsql;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed partial class ChargeRepository
{
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

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

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
}
