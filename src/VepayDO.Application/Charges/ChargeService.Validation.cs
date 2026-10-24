using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Charges;

public sealed partial class ChargeService
{
    private async Task<ChargeView> RequireViewAsync(Guid tenantId, Guid chargeId, CancellationToken cancellationToken)
    {
        var record = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Charge not found");
        return ToView(record);
    }

    private Task WriteAuditAsync(Guid tenantId, Guid? entityId, string action, object? before, object? after, RequestMeta meta, CancellationToken cancellationToken, string? reason = null)
    {
        var builder = new AuditEventBuilder().TenantId(tenantId).UserId(tenantContext.Current?.UserId).Entity("charge").Action(action).Before(before).After(after).Ip(meta.Ip);
        if (entityId is not null) builder.EntityId(entityId);
        if (reason is not null) builder.Reason(reason);
        return audit.WriteAsync(builder.Build(), cancellationToken);
    }

    private static string RequireDescription(string? description) => string.IsNullOrWhiteSpace(description) ? throw new InvalidArgumentDomainException("Description is required") : (description.Trim().Length > 500 ? throw new InvalidArgumentDomainException("Description must be at most 500 characters") : description.Trim());
    private static decimal RequireAmount(decimal amount) => amount <= 0m ? throw new InvalidArgumentDomainException("Amount must be greater than zero") : decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    private static string RequireReason(string? reason) => string.IsNullOrWhiteSpace(reason) ? throw new InvalidArgumentDomainException("A reason is required") : reason.Trim();
    private static void ValidateDates(DateOnly issuedOn, DateOnly? dueOn) { if (dueOn is not null && dueOn.Value < issuedOn) throw new InvalidArgumentDomainException("Due date cannot be before the issue date"); }
    private static void ValidateStatus(string? status) { if (status is not null && !Enum.TryParse<ChargeStatus>(status, ignoreCase: true, out _)) throw new InvalidArgumentDomainException($"Status must be one of {string.Join(", ", Enum.GetNames<ChargeStatus>())}"); }
    private static ChargeView ToView(ChargeRecord r) => new(r.Id, r.CustomerId, r.Code, r.Description, r.Amount, r.Currency, r.IssuedOn, r.DueOn, r.Status, r.AmountApplied, r.Balance, r.VoidedAt, r.VoidReason, r.CreatedAt, r.UpdatedAt);
    private Guid RequireTenantId() => tenantContext.Current?.TenantId is { } id && id != Guid.Empty ? id : throw new MissingTenantContextException();
}
