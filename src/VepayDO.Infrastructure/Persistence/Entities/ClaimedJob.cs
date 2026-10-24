namespace VepayDO.Infrastructure.Persistence.Entities;

/// <summary>
/// A job claimed by the worker, as returned by the queue claim query. The type
/// is unmapped: EF Core projects the raw columns of the claiming statement onto
/// it (M5.4).
/// </summary>
public sealed class ClaimedJob
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Kind { get; set; } = null!;

    public short Attempts { get; set; }

    public DateTimeOffset AvailableAt { get; set; }
}
