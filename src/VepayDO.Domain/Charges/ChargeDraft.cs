using VepayDO.Domain.Common;
using VepayDO.Domain.Enums;

namespace VepayDO.Domain.Charges;

public sealed record ChargeDraft : ICloneable<ChargeDraft>
{
    public ChargeDraft(
        Guid? id,
        Guid? customerId,
        ChargeCode code,
        string description,
        Money amount,
        DateOnly issuedOn,
        DateOnly? dueOn,
        ChargeStatus status,
        Guid? createdBy,
        IReadOnlyList<ChargeAllocationDraft> allocations)
    {
        Id = id;
        CustomerId = customerId;
        Code = code;
        Description = description;
        Amount = amount;
        IssuedOn = issuedOn;
        DueOn = dueOn;
        Status = status;
        CreatedBy = createdBy;
        Allocations = allocations;
    }

    public Guid? Id { get; init; }

    public Guid? CustomerId { get; init; }

    public ChargeCode Code { get; init; }

    public string Description { get; init; }

    public Money Amount { get; init; }

    public DateOnly IssuedOn { get; init; }

    public DateOnly? DueOn { get; init; }

    public ChargeStatus Status { get; init; }

    public Guid? CreatedBy { get; init; }

    public IReadOnlyList<ChargeAllocationDraft> Allocations { get; init; }

    ChargeDraft ICloneable<ChargeDraft>.Clone() =>
        new(
            id: null,
            customerId: CustomerId,
            code: Code,
            description: Description,
            amount: new Money(Amount.Amount, Amount.Currency),
            issuedOn: IssuedOn,
            dueOn: DueOn,
            status: ChargeStatus.OPEN,
            createdBy: CreatedBy,
            allocations: []);

    public static ChargeDraft Create(
        Guid? customerId,
        ChargeCode code,
        string description,
        Money amount,
        DateOnly issuedOn,
        DateOnly? dueOn,
        Guid? createdBy) =>
        new(
            id: null,
            customerId: customerId,
            code: code,
            description: description,
            amount: new Money(amount.Amount, amount.Currency),
            issuedOn: issuedOn,
            dueOn: dueOn,
            status: ChargeStatus.OPEN,
            createdBy: createdBy,
            allocations: []);
}

public sealed record ChargeAllocationDraft(Guid ReceiptId, Money AmountApplied);
