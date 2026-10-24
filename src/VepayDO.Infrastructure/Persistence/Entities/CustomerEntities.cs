namespace VepayDO.Infrastructure.Persistence.Entities;

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

public sealed class CustomerAlias
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string AliasNormalized { get; set; } = null!;
}
