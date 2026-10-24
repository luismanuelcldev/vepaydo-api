using VepayDO.Application.Auth;
using VepayDO.Application.Charges.Ports;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Charges;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Charges;

public sealed partial class ChargeService
{
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

                valid.Add(new NewCharge(row.CustomerId, code, description, amount, currency, row.IssuedOn, row.DueOn, createdBy));
            }
            catch (DomainException ex)
            {
                errors.Add(new ChargeImportRowError(row.RowNumber, ex.Message));
            }
        }

        if (errors.Count > 0 && allOrNothing)
        {
            await WriteAuditAsync(tenantId, entityId: null, "import", before: null, after: new { imported = 0, skipped = rows.Count, allOrNothing = true }, meta, cancellationToken).ConfigureAwait(false);
            return new ChargeImportResult(Imported: 0, Skipped: rows.Count, AllOrNothing: true, errors);
        }

        if (valid.Count > 0)
        {
            await charges.AddManyAsync(tenantId, valid, cancellationToken).ConfigureAwait(false);
        }

        await WriteAuditAsync(tenantId, entityId: null, "import", before: null, after: new { imported = valid.Count, skipped = rows.Count - valid.Count, allOrNothing }, meta, cancellationToken).ConfigureAwait(false);
        return new ChargeImportResult(Imported: valid.Count, Skipped: rows.Count - valid.Count, AllOrNothing: allOrNothing, errors);
    }
}
