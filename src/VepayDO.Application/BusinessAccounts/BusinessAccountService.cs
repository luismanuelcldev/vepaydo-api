using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.BusinessAccounts;

public sealed partial class BusinessAccountService(
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

    private static string RequireLabel(string? label)
    {
        var trimmed = label?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) throw new InvalidArgumentDomainException("Label is required");
        if (trimmed.Length > 120) throw new InvalidArgumentDomainException("Label must be at most 120 characters");
        return trimmed;
    }

    private static BusinessAccountView ToView(BusinessAccountRecord record) =>
        new(record.Id, record.BankId, record.BankName, record.Label, record.Last4, record.Currency, record.IsActive, record.CreatedAt, record.UpdatedAt);

    private Guid RequireTenantId()
    {
        var tenantId = tenantContext.Current?.TenantId;
        if (tenantId is null || tenantId == Guid.Empty) throw new MissingTenantContextException();
        return tenantId.Value;
    }
}
