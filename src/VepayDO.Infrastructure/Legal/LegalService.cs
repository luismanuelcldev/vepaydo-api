using Microsoft.EntityFrameworkCore;
using VepayDO.Application.Configuration;
using VepayDO.Application.Legal;
using VepayDO.Domain.Enums;
using VepayDO.Infrastructure.Persistence;

namespace VepayDO.Infrastructure.Legal;

/// <summary>
/// Reads the immutable <c>legal_acceptances</c> ledger. The table is global
/// (no <c>tenant_id</c>, no RLS - AGENTS.md section 4.5), so a plain read
/// outside any tenant transaction is correct here.
/// </summary>
public sealed class LegalService(AppDbContext db, AppConfig config) : ILegalService
{
    public async Task<bool> HasAcceptedAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        return await IsAcceptedAsync(userId, LegalDocumentType.TERMS, config.LegalTermsVersion, cancellationToken)
            .ConfigureAwait(false)
            && await IsAcceptedAsync(userId, LegalDocumentType.PRIVACY, config.LegalPrivacyVersion, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<bool> IsAcceptedAsync(
        Guid userId,
        LegalDocumentType documentType,
        string version,
        CancellationToken cancellationToken)
    {
        var accepted = await db.LegalAcceptances
            .AnyAsync(
                acceptance =>
                    acceptance.UserId == userId &&
                    acceptance.DocumentType == documentType &&
                    acceptance.Version == version,
                cancellationToken)
            .ConfigureAwait(false);

        return accepted;
    }
}
