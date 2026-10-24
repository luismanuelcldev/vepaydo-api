using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Charges;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Charges;

/// <summary>
/// Charge use cases (UC-DATA-03 to 06). Amounts are validated as positive
/// <see cref="Money"/> in a supported currency; dates keep <c>dueOn &gt;= issuedOn</c>;
/// a voided charge is immutable. Charge codes are unique per tenant and are
/// generated (and retried on collision) when not supplied. Every mutation writes
/// exactly one audit event in the same transaction (AGENTS.md sections 9.2, 9.8).
/// </summary>
public sealed class ChargeService(
    IChargeRepository charges,
    IChargeCodeSource codes,
    IAuditWriter audit,
    IClock clock,
    ITenantContextAccessor tenantContext) : IChargeService
{
    private const int MaxCodeAttempts = 5;

    public async Task<IReadOnlyList<ChargeView>> ListAsync(
        ChargeQuery query,
        CancellationToken cancellationToken = default)
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

    public async Task<ChargeView> CreateAsync(
        CreateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var description = RequireDescription(input.Description);
        var amount = RequireAmount(input.Amount);
        var currency = Currencies.Require(input.Currency);
        ValidateDates(input.IssuedOn, input.DueOn);

        var createdBy = tenantContext.Current?.UserId;

        var id = await InsertWithUniqueCodeAsync(
            tenantId,
            input.Code,
            code => new NewCharge(
                input.CustomerId, code, description, amount, currency, input.IssuedOn, input.DueOn, createdBy),
            cancellationToken,
            hint: description).ConfigureAwait(false);

        await WriteAuditAsync(
            tenantId,
            id,
            "create",
            before: null,
            after: new { code = input.Code, amount, currency, issuedOn = input.IssuedOn },
            meta,
            cancellationToken).ConfigureAwait(false);

        return await RequireViewAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChargeView> UpdateAsync(
        Guid chargeId,
        UpdateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var current = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Charge not found");

        if (string.Equals(current.Status, nameof(ChargeStatus.VOIDED), StringComparison.Ordinal))
        {
            throw new ConflictException("A voided charge cannot be edited");
        }

        var description = input.Description is null ? current.Description : RequireDescription(input.Description);
        var amount = input.Amount is null ? current.Amount : RequireAmount(input.Amount.Value);
        var currency = input.Currency is null ? current.Currency : Currencies.Require(input.Currency);
        var issuedOn = input.IssuedOn ?? current.IssuedOn;
        var dueOn = input.DueOn ?? current.DueOn;
        ValidateDates(issuedOn, dueOn);

        var code = input.Code is null ? current.Code : ChargeCode.Parse(input.Code).Value;
        if (!string.Equals(code, current.Code, StringComparison.Ordinal)
            && await charges.CodeExistsAsync(tenantId, code, chargeId, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException("Charge code already exists in this tenant");
        }

        var customerId = input.CustomerId ?? current.CustomerId;

        await charges.UpdateAsync(
            tenantId,
            chargeId,
            new UpdateChargeFields(customerId, code, description, amount, currency, issuedOn, dueOn),
            cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(
            tenantId,
            chargeId,
            "update",
            before: new { code = current.Code, description = current.Description, amount = current.Amount },
            after: new { code, description, amount, currency, issuedOn, dueOn },
            meta,
            cancellationToken).ConfigureAwait(false);

        return await RequireViewAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChargeView> VoidAsync(
        Guid chargeId,
        VoidChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var reason = RequireReason(input.Reason);
        var current = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                      ?? throw new NotFoundException("Charge not found");

        if (string.Equals(current.Status, nameof(ChargeStatus.VOIDED), StringComparison.Ordinal))
        {
            throw new ConflictException("Charge is already voided");
        }

        var voidedBy = tenantContext.Current?.UserId;
        await charges.VoidAsync(tenantId, chargeId, reason, voidedBy, cancellationToken).ConfigureAwait(false);

        await WriteAuditAsync(
            tenantId,
            chargeId,
            "void",
            before: new { status = current.Status },
            after: new { status = nameof(ChargeStatus.VOIDED) },
            meta,
            cancellationToken,
            reason).ConfigureAwait(false);

        return await RequireViewAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChargeView> DuplicateAsync(
        Guid chargeId,
        DuplicateChargeInput input,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var source = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Charge not found");

        // Prototype: build a draft of the original and clone it, so identity,
        // status and allocations are cleared (UC-DATA-04).
        var original = new ChargeDraft(
            id: source.Id,
            customerId: source.CustomerId,
            code: ChargeCode.Parse(source.Code),
            description: source.Description,
            amount: new Money(source.Amount, source.Currency),
            issuedOn: source.IssuedOn,
            dueOn: source.DueOn,
            status: Enum.Parse<ChargeStatus>(source.Status, ignoreCase: false),
            createdBy: tenantContext.Current?.UserId,
            allocations: []);

        var duplicate = ((Domain.Common.ICloneable<ChargeDraft>)original).Clone();
        var createdBy = tenantContext.Current?.UserId;
        var today = clock.Today(TenantTimeZone.Get(TenantTimeZone.Default));

        var id = await InsertWithUniqueCodeAsync(
            tenantId,
            input.Code,
            code => new NewCharge(
                duplicate.CustomerId,
                code,
                duplicate.Description,
                duplicate.Amount.Amount,
                duplicate.Amount.Currency,
                today,
                null,
                createdBy),
            cancellationToken,
            hint: duplicate.Description).ConfigureAwait(false);

        await WriteAuditAsync(
            tenantId,
            id,
            "duplicate",
            before: new { source = source.Id },
            after: new { duplicatedFrom = source.Id },
            meta,
            cancellationToken).ConfigureAwait(false);

        return await RequireViewAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChargeImportResult> ImportAsync(
        IReadOnlyList<ChargeImportRow> rows,
        bool allOrNothing,
        RequestMeta meta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var createdBy = tenantContext.Current?.UserId;
        var errors = new List<ChargeImportRowError>();
        var valid = new List<NewCharge>();

        foreach (var row in rows)
        {
            try
            {
                var description = RequireDescription(row.Description);
                var amount = RequireAmount(row.Amount);
                var currency = Currencies.Require(row.Currency);
                ValidateDates(row.IssuedOn, row.DueOn);

                var code = row.Code is null
                    ? await GenerateCodeAsync(tenantId, description, cancellationToken).ConfigureAwait(false)
                    : ChargeCode.Parse(row.Code).Value;

                valid.Add(new NewCharge(
                    row.CustomerId, code, description, amount, currency, row.IssuedOn, row.DueOn, createdBy));
            }
            catch (DomainException ex)
            {
                errors.Add(new ChargeImportRowError(row.RowNumber, ex.Message));
            }
        }

        if (errors.Count > 0 && allOrNothing)
        {
            await WriteAuditAsync(
                tenantId,
                entityId: null,
                "import",
                before: null,
                after: new { imported = 0, skipped = rows.Count, allOrNothing = true },
                meta,
                cancellationToken).ConfigureAwait(false);

            return new ChargeImportResult(Imported: 0, Skipped: rows.Count, AllOrNothing: true, errors);
        }

        if (valid.Count > 0)
        {
            await charges.AddManyAsync(tenantId, valid, cancellationToken).ConfigureAwait(false);
        }

        await WriteAuditAsync(
            tenantId,
            entityId: null,
            "import",
            before: null,
            after: new { imported = valid.Count, skipped = rows.Count - valid.Count, allOrNothing },
            meta,
            cancellationToken).ConfigureAwait(false);

        return new ChargeImportResult(
            Imported: valid.Count,
            Skipped: rows.Count - valid.Count,
            AllOrNothing: allOrNothing,
            errors);
    }

    private async Task<Guid> InsertWithUniqueCodeAsync(
        Guid tenantId,
        string? requestedCode,
        Func<string, NewCharge> build,
        CancellationToken cancellationToken,
        string? hint = null)
    {
        if (requestedCode is not null)
        {
            var explicitCode = ChargeCode.Parse(requestedCode).Value;
            if (await charges.CodeExistsAsync(tenantId, explicitCode, null, cancellationToken).ConfigureAwait(false))
            {
                throw new ConflictException("Charge code already exists in this tenant");
            }

            return await charges.AddAsync(tenantId, build(explicitCode), cancellationToken).ConfigureAwait(false);
        }

        // Generated codes are checked against the tenant before the insert; the
        // (tenant_id, code) unique index is the backstop for a concurrent insert
        // that wins the race, which the repository surfaces as a ConflictException
        // and the loop answers with a freshly generated code.
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var generated = await GenerateCodeAsync(tenantId, hint, cancellationToken).ConfigureAwait(false);

            try
            {
                return await charges.AddAsync(tenantId, build(generated), cancellationToken).ConfigureAwait(false);
            }
            catch (ConflictException) when (attempt < MaxCodeAttempts - 1)
            {
                // Retry with a new candidate code.
            }
        }

        throw new ConflictException("Could not generate a unique charge code");
    }

    private async Task<string> GenerateCodeAsync(Guid tenantId, string? hint, CancellationToken cancellationToken)
    {
        var prefix = CodePrefixFrom(hint);
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var candidate = ChargeCode.Generate(prefix, codes.NextValue).Value;
            if (!await charges.CodeExistsAsync(tenantId, candidate, null, cancellationToken).ConfigureAwait(false))
            {
                return candidate;
            }
        }

        throw new ConflictException("Could not generate a unique charge code");
    }

    private static string CodePrefixFrom(string? hint)
    {
        var description = hint?.Trim() ?? string.Empty;
        if (description.Length == 0)
        {
            return "C";
        }

        var first = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return first.Length >= 3 ? first[..3] : first;
    }

    private async Task<ChargeView> RequireViewAsync(Guid tenantId, Guid chargeId, CancellationToken cancellationToken)
    {
        var record = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Charge not found");

        return ToView(record);
    }

    private Task WriteAuditAsync(
        Guid tenantId,
        Guid? entityId,
        string action,
        object? before,
        object? after,
        RequestMeta meta,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var builder = new AuditEventBuilder()
            .TenantId(tenantId)
            .UserId(tenantContext.Current?.UserId)
            .Entity("charge")
            .Action(action)
            .Before(before)
            .After(after)
            .Ip(meta.Ip);

        if (entityId is not null)
        {
            builder.EntityId(entityId);
        }

        if (reason is not null)
        {
            builder.Reason(reason);
        }

        return audit.WriteAsync(builder.Build(), cancellationToken);
    }

    private static string RequireDescription(string? description)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvalidArgumentDomainException("Description is required");
        }

        if (trimmed.Length > 500)
        {
            throw new InvalidArgumentDomainException("Description must be at most 500 characters");
        }

        return trimmed;
    }

    private static decimal RequireAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new InvalidArgumentDomainException("Amount must be greater than zero");
        }

        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static string RequireReason(string? reason)
    {
        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvalidArgumentDomainException("A reason is required");
        }

        return trimmed;
    }

    private static void ValidateDates(DateOnly issuedOn, DateOnly? dueOn)
    {
        if (dueOn is not null && dueOn.Value < issuedOn)
        {
            throw new InvalidArgumentDomainException("Due date cannot be before the issue date");
        }
    }

    private static void ValidateStatus(string? status)
    {
        if (status is null)
        {
            return;
        }

        if (!Enum.TryParse<ChargeStatus>(status, ignoreCase: true, out _))
        {
            throw new InvalidArgumentDomainException(
                $"Status must be one of {string.Join(", ", Enum.GetNames<ChargeStatus>())}");
        }
    }

    private static ChargeView ToView(ChargeRecord record) =>
        new(
            record.Id,
            record.CustomerId,
            record.Code,
            record.Description,
            record.Amount,
            record.Currency,
            record.IssuedOn,
            record.DueOn,
            record.Status,
            record.AmountApplied,
            record.Balance,
            record.VoidedAt,
            record.VoidReason,
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
