namespace VepayDO.Infrastructure.Persistence.Entities;

/// <summary>
/// A queued processing job. Maps to <c>processing_jobs</c>. The job id is the
/// receipt id, so enqueuing twice is harmless and a retry is idempotent
/// (M5.4). The table carries no business data: only identifiers and state.
/// </summary>
public sealed class ProcessingJob
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public short Attempts { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
