namespace VepayDO.Infrastructure.Persistence.Entities;

public sealed class ClaimedJob
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Kind { get; set; } = null!;

    public short Attempts { get; set; }

    public DateTimeOffset AvailableAt { get; set; }
}
