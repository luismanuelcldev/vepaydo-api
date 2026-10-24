namespace VepayDO.Infrastructure.Persistence.Entities;

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
