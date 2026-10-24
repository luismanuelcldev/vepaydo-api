using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.BusinessAccounts;

/// <summary>
/// Business-account use cases (UC-SET-03). Only the last four digits are ever
/// accepted or returned; the bank must exist; the (bank, last4, currency) triple
/// is unique per tenant and the duplicate is surfaced as a controlled domain
/// error before the database constraint is hit. Every mutation is audited in the
/// same transaction (AGENTS.md sections 7, 9.8).
/// </summary>
public sealed class BusinessAccountService(
    IBusinessAccountRepository accounts,
    IBankRepository banks,
    IAuditWriter audit,
    IClock clock,
    ITenantContextAccessor tenantContext) : IBusinessAccountService
{
    public async Task<IReadOnlyList<BusinessAccountView>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var records = await accounts.ListAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return records.Select(ToView).ToList();
    }

    public async Task<BusinessAccountView> GetAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var record = await accounts.FindAsync(tenantId, accountId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Business account not found");

        return ToView(record);
    }

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

        if (await accounts
                .ExistsDuplicateAsync(tenantId, input.BankId, last4, currency, excludingAccountId: null, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException("An account with the same bank and last four digits already exists");
        }

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

        if (newLabel == current.Label && newActive == current.IsActive)
        {
            return ToView(current);
        }

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

    private static string RequireLabel(string? label)
    {
        var trimmed = label?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            throw new InvalidArgumentDomainException("Label is required");
        }

        if (trimmed.Length > 120)
        {
            throw new InvalidArgumentDomainException("Label must be at most 120 characters");
        }

        return trimmed;
    }

    private static BusinessAccountView ToView(BusinessAccountRecord record) =>
        new(
            record.Id,
            record.BankId,
            record.BankName,
            record.Label,
            record.Last4,
            record.Currency,
            record.IsActive,
            record.CreatedAt,
            record.UpdatedAt);

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            throw new MissingTenantContextException();
        }

        return tenantId.Value;
    }
}
