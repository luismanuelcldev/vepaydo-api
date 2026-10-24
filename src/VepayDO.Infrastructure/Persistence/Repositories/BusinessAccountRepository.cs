using Microsoft.EntityFrameworkCore;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

public sealed class BusinessAccountRepository(TxProvider tx, IClock clock) : IBusinessAccountRepository
{
    public async Task<IReadOnlyList<BusinessAccountRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await tx.DbContext.BusinessAccounts.AsNoTracking().Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.IsActive).ThenBy(a => a.Label).ThenBy(a => a.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var bankNames = await BankNamesAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(a => ToRecord(a, bankNames)).ToList();
    }

    public async Task<BusinessAccountRecord?> FindAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var row = await tx.DbContext.BusinessAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId, cancellationToken).ConfigureAwait(false);
        if (row is null) return null;
        var bankNames = await BankNamesAsync(cancellationToken).ConfigureAwait(false);
        return ToRecord(row, bankNames);
    }

    public async Task<bool> ExistsDuplicateAsync(Guid tenantId, short bankId, string last4, string currency, Guid? excludingAccountId, CancellationToken cancellationToken = default)
    {
        return await tx.DbContext.BusinessAccounts.AsNoTracking().AnyAsync(
            a => a.TenantId == tenantId && a.BankId == bankId && a.Last4 == last4 && a.Currency == currency && (excludingAccountId == null || a.Id != excludingAccountId.Value),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid> AddAsync(Guid tenantId, NewBusinessAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account, nameof(account));
        var now = clock.UtcNow;
        var entity = new BusinessAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BankId = account.BankId, Label = account.Label,
            Last4 = account.Last4, Currency = account.Currency, IsActive = account.IsActive, CreatedAt = now, UpdatedAt = now,
        };
        tx.DbContext.BusinessAccounts.Add(entity);
        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid tenantId, Guid accountId, string label, bool isActive, CancellationToken cancellationToken = default)
    {
        await tx.DbContext.BusinessAccounts.Where(a => a.TenantId == tenantId && a.Id == accountId).ExecuteUpdateAsync(
            s => s.SetProperty(a => a.Label, label).SetProperty(a => a.IsActive, isActive).SetProperty(a => a.UpdatedAt, clock.UtcNow),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<short, string>> BankNamesAsync(CancellationToken cancellationToken) =>
        await tx.DbContext.Banks.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken).ConfigureAwait(false);

    private static BusinessAccountRecord ToRecord(BusinessAccount account, Dictionary<short, string> bankNames) =>
        new(account.Id, account.BankId, bankNames.TryGetValue(account.BankId, out var name) ? name : string.Empty, account.Label, account.Last4, account.Currency, account.IsActive, account.CreatedAt, account.UpdatedAt);
}
