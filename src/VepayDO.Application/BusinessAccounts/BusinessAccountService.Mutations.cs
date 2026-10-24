using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.BusinessAccounts;

public sealed partial class BusinessAccountService
{
    public async Task<BusinessAccountView> CreateAsync(
        CreateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var label = RequireLabel(input.Label);
        var last4 = AccountLast4.Parse(input.Last4).Value;
        var currency = Currencies.Require(input.Currency);

        var bank = await banks.FindActiveAsync(input.BankId, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidArgumentDomainException("Unknown or inactive bank");

        if (await accounts.ExistsDuplicateAsync(tenantId, input.BankId, last4, currency, excludingAccountId: null, cancellationToken).ConfigureAwait(false))
            throw new ConflictException("An account with the same bank and last four digits already exists");

        var newAccount = new NewBusinessAccount(input.BankId, label, last4, currency, IsActive: true);
        var id = await accounts.AddAsync(tenantId, newAccount, cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("business_account")
                .EntityId(id)
                .Action("create")
                .After(new { id, bankId = input.BankId, label, last4, currency, isActive = true })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        return new BusinessAccountView(id, input.BankId, bank.Name, label, last4, currency, IsActive: true, now, now);
    }

    public async Task<BusinessAccountView> UpdateAsync(
        Guid accountId,
        UpdateBusinessAccountInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var current = await accounts.FindAsync(tenantId, accountId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Business account not found");

        var newLabel = input.Label is null ? current.Label : RequireLabel(input.Label);
        var newActive = input.IsActive ?? current.IsActive;

        if (newLabel == current.Label && newActive == current.IsActive) return ToView(current);

        await accounts.UpdateAsync(tenantId, accountId, newLabel, newActive, cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
            new AuditEventBuilder()
                .TenantId(tenantId)
                .UserId(tenantContext.Current?.UserId)
                .Entity("business_account")
                .EntityId(accountId)
                .Action("update")
                .Before(new { label = current.Label, isActive = current.IsActive })
                .After(new { label = newLabel, isActive = newActive })
                .Ip(meta.Ip)
                .Build(),
            cancellationToken).ConfigureAwait(false);

        return ToView(current with { Label = newLabel, IsActive = newActive, UpdatedAt = clock.UtcNow });
    }
}
