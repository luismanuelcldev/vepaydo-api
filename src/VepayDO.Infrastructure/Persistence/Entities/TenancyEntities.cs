namespace VepayDO.Infrastructure.Persistence.Entities;

using VepayDO.Domain.Enums;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Rnc { get; set; }
    public string TimeZone { get; set; } = null!;
    public TenantPlan Plan { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TenantSettings
{
    public Guid TenantId { get; set; }
    public short DateWindowDays { get; set; }
    public decimal AmountTolerance { get; set; }
    public decimal? VerifierAmountCap { get; set; }
    public short ImageRetentionMonths { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Membership
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
