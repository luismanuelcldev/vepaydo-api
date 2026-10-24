using VepayDO.Domain.Enums;

namespace VepayDO.Infrastructure.Persistence.Entities;

public sealed class Charge
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Code { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly IssuedOn { get; set; }
    public DateOnly? DueOn { get; set; }
    public ChargeStatus Status { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ChargeBalanceRow
{
    public Guid TenantId { get; set; }
    public Guid ChargeId { get; set; }
    public string Code { get; set; } = null!;
    public Guid? CustomerId { get; set; }
    public string Description { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly IssuedOn { get; set; }
    public DateOnly? DueOn { get; set; }
    public ChargeStatus Status { get; set; }
    public decimal AmountApplied { get; set; }
    public decimal Balance { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
