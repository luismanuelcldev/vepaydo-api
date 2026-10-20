using VepayDO.Application.Audit;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Persistence.Entities;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure.Persistence.Repositories;

/// <summary>
/// Append-only writer for <c>audit_events</c>. The table is RLS-protected and
/// requires a tenant context plus the owner/system role (AGENTS.md section 7);
/// callers therefore only write the audit row inside the matching transaction.
/// </summary>
public sealed class AuditEventRepository(TxProvider tx, IClock clock) : IAuditWriter
{
    public async Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        tx.DbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = draft.TenantId,
            UserId = draft.UserId,
            Actor = draft.Actor,
            Entity = draft.Entity,
            EntityId = draft.EntityId,
            Action = draft.Action,
            Before = draft.Before,
            After = draft.After,
            Reason = draft.Reason,
            Ip = draft.Ip,
            CreatedAt = clock.UtcNow,
        });

        await tx.DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
