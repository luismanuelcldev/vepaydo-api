using System.Net;
using VepayDO.Domain.Enums;

namespace VepayDO.Application.Audit;

/// <summary>
/// An immutable audit entry about to be written. <see cref="Before"/> and
/// <see cref="After"/> are JSON snapshots; the entity stores them verbatim
/// (AGENTS.md section 9.8).
/// </summary>
public sealed record AuditEventDraft
{
    public required Guid TenantId { get; init; }

    public Guid? UserId { get; init; }

    public AuditActor Actor { get; init; } = AuditActor.USER;

    public required string Entity { get; init; }

    public Guid? EntityId { get; init; }

    public required string Action { get; init; }

    public string? Before { get; init; }

    public string? After { get; init; }

    public string? Reason { get; init; }

    public IPAddress? Ip { get; init; }
}

/// <summary>
/// Append-only audit writer. Every state change, correction, decision and
/// alert resolution is written in the same transaction as the change it
/// records (AGENTS.md section 9.8).
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default);
}
