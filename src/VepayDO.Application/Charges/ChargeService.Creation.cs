using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Charges;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Charges;

public sealed partial class ChargeService
{
    public async Task<ChargeView> CreateAsync(CreateChargeInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var description = RequireDescription(input.Description);
        var amount = RequireAmount(input.Amount);
        var currency = Currencies.Require(input.Currency);
        ValidateDates(input.IssuedOn, input.DueOn);

        var createdBy = tenantContext.Current?.UserId;
        var id = await InsertWithUniqueCodeAsync(tenantId, input.Code, code => new NewCharge(input.CustomerId, code, description, amount, currency, input.IssuedOn, input.DueOn, createdBy), cancellationToken, hint: description).ConfigureAwait(false);

        await WriteAuditAsync(tenantId, id, "create", before: null, after: new { code = input.Code, amount, currency, issuedOn = input.IssuedOn }, meta, cancellationToken).ConfigureAwait(false);
        return await RequireViewAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ChargeView> DuplicateAsync(Guid chargeId, DuplicateChargeInput input, RequestMeta meta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(meta);

        var tenantId = RequireTenantId();
        var source = await charges.FindAsync(tenantId, chargeId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("Charge not found");

        var original = new ChargeDraft(source.Id, source.CustomerId, ChargeCode.Parse(source.Code), source.Description, new Money(source.Amount, source.Currency), source.IssuedOn, source.DueOn, Enum.Parse<ChargeStatus>(source.Status, ignoreCase: false), tenantContext.Current?.UserId, []);
        var duplicate = ((Domain.Common.ICloneable<ChargeDraft>)original).Clone();
        var createdBy = tenantContext.Current?.UserId;
        var today = clock.Today(TenantTimeZone.Get(TenantTimeZone.Default));

        var id = await InsertWithUniqueCodeAsync(tenantId, input.Code, code => new NewCharge(duplicate.CustomerId, code, duplicate.Description, duplicate.Amount.Amount, duplicate.Amount.Currency, today, null, createdBy), cancellationToken, hint: duplicate.Description).ConfigureAwait(false);

        await WriteAuditAsync(tenantId, id, "duplicate", before: new { source = source.Id }, after: new { duplicatedFrom = source.Id }, meta, cancellationToken).ConfigureAwait(false);
        return await RequireViewAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Guid> InsertWithUniqueCodeAsync(Guid tenantId, string? requestedCode, Func<string, NewCharge> build, CancellationToken cancellationToken, string? hint = null)
    {
        if (requestedCode is not null)
        {
            var explicitCode = ChargeCode.Parse(requestedCode).Value;
            if (await charges.CodeExistsAsync(tenantId, explicitCode, null, cancellationToken).ConfigureAwait(false))
                throw new ConflictException("Charge code already exists in this tenant");

            return await charges.AddAsync(tenantId, build(explicitCode), cancellationToken).ConfigureAwait(false);
        }

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var generated = await GenerateCodeAsync(tenantId, hint, cancellationToken).ConfigureAwait(false);
            try { return await charges.AddAsync(tenantId, build(generated), cancellationToken).ConfigureAwait(false); }
            catch (ConflictException) when (attempt < MaxCodeAttempts - 1) { }
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
                return candidate;
        }

        throw new ConflictException("Could not generate a unique charge code");
    }

    private static string CodePrefixFrom(string? hint)
    {
        var description = hint?.Trim() ?? string.Empty;
        if (description.Length == 0) return "C";
        var first = description.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return first.Length >= 3 ? first[..3] : first;
    }
}
