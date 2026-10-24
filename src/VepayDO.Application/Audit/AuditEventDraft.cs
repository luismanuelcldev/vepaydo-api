using System.Net;
using VepayDO.Domain.Enums;

namespace VepayDO.Application.Audit;

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

public interface IAuditWriter
{
    Task WriteAsync(AuditEventDraft draft, CancellationToken cancellationToken = default);
}
