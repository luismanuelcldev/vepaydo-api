using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Charges;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Charges;

public sealed partial class ChargeService(
    IChargeRepository charges,
    IChargeCodeSource codes,
    IAuditWriter audit,
    IClock clock,
    ITenantContextAccessor tenantContext) : IChargeService
{
    private const int MaxCodeAttempts = 5;

    public async Task<IReadOnlyList<ChargeView>> ListAsync(ChargeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var tenantId = RequireTenantId();
        ValidateStatus(query.Status);
        var records = await charges.ListAsync(tenantId, query, cancellationToken).ConfigureAwait(false);
        return records.Select(ToView).ToList();
    }

    public async Task<ChargeView> GetAsync(Guid chargeId, CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var record = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Charge not found");
        return ToView(record);
    }

    public async Task<ChargeView> UpdateAsync(Guid chargeId, UpdateChargeInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var current = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Charge not found");

        if (string.Equals(current.Status, nameof(ChargeStatus.VOIDED), StringComparison.Ordinal))
            throw new ConflictException("A voided charge cannot be edited");

        var description = input.Description is null ? current.Description : RequireDescription(input.Description);
        var amount = input.Amount is null ? current.Amount : RequireAmount(input.Amount.Value);
        var currency = input.Currency is null ? current.Currency : Currencies.Require(input.Currency);
        var issuedOn = input.IssuedOn ?? current.IssuedOn;
        var dueOn = input.DueOn ?? current.DueOn;
        ValidateDates(issuedOn, dueOn);

        var code = input.Code is null ? current.Code : ChargeCode.Parse(input.Code).Value;
        if (!string.Equals(code, current.Code, StringComparison.Ordinal)
            && await charges.CodeExistsAsync(tenantId, code, chargeId, cancellationToken).ConfigureAwait(false))
            throw new ConflictException("Charge code already exists in this tenant");

        var customerId = input.CustomerId ?? current.CustomerId;

        await charges.UpdateAsync(tenantId, chargeId, new UpdateChargeFields(customerId, code, description, amount, currency, issuedOn, dueOn), cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(tenantId, chargeId, "update", before: new { code = current.Code, description = current.Description, amount = current.Amount }, after: new { code, description, amount, currency, issuedOn, dueOn }, meta, cancellationToken).ConfigureAwait(false);

        return await RequireViewAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChargeView> VoidAsync(Guid chargeId, VoidChargeInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var reason = RequireReason(input.Reason);
        var current = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Charge not found");

        if (string.Equals(current.Status, nameof(ChargeStatus.VOIDED), StringComparison.Ordinal))
            throw new ConflictException("Charge is already voided");

        var voidedBy = tenantContext.Current?.UserId;
        await charges.VoidAsync(tenantId, chargeId, reason, voidedBy, cancellationToken).ConfigureAwait(false);
        await WriteAuditAsync(tenantId, chargeId, "void", before: new { status = current.Status }, after: new { status = nameof(ChargeStatus.VOIDED) }, meta, cancellationToken, reason).ConfigureAwait(false);

        return await RequireViewAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false);
    }
}
