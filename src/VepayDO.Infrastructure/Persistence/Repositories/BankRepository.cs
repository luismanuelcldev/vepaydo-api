using Microsoft.EntityFrameworkCore;

using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only access to the global <c>banks</c> catalogue (no RLS). Used to
/// validate a business account's bank before insert.
/// </summary>
public sealed class BankRepository(TxProvider tx) : IBankRepository
{
    public async Task<BankRecord?> FindActiveAsync(short bankId, CancellationToken cancellationToken = default)
    {
        var bank = await tx.DbContext.Banks
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bankId && b.IsActive, cancellationToken)
            .ConfigureAwait(false);

        return bank is null ? null : new BankRecord(bank.Id, bank.Code, bank.Name, bank.IsActive);
    }
}
