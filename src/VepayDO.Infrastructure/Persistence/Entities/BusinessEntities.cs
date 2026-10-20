using VepayDO.Domain.Enums;

namespace VepayDO.Infrastructure.Persistence.Entities;

/// <summary>Business bank account (last 4 digits only). Maps to <c>business_accounts</c>.</summary>
public sealed class BusinessAccount
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public short BankId { get; set; }

    public string Label { get; set; } = null!;

    public string Last4 { get; set; } = null!;

    public string Currency { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Customer. Maps to <c>customers</c>.</summary>
public sealed class Customer
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string NameNormalized { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Sender alias used to match transfers. Maps to <c>customer_aliases</c>.</summary>
public sealed class CustomerAlias
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public string AliasNormalized { get; set; } = null!;
}

/// <summary>Expected collection. Maps to <c>charges</c>.</summary>
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
