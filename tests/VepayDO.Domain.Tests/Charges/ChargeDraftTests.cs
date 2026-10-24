using FluentAssertions;

using VepayDO.Domain;
using VepayDO.Domain.Charges;
using VepayDO.Domain.Common;
using VepayDO.Domain.Enums;

namespace VepayDO.Domain.Tests.Charges;

/// <summary>
/// Unit tests for the <see cref="ChargeDraft"/> Prototype (UC-DATA-04, AGENTS.md
/// section 8.4): cloning yields an independent draft with a fresh identity, a
/// cleared status and no allocations, and no mutable state is shared.
/// </summary>
public sealed class ChargeDraftTests
{
    private static ChargeDraft Original() =>
        new(
            id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            customerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            code: ChargeCode.Parse("FACT-001"),
            description: "Servicio de marzo",
            amount: new Money(1500.50m, "DOP"),
            issuedOn: new DateOnly(2026, 3, 1),
            dueOn: new DateOnly(2026, 3, 31),
            status: ChargeStatus.PARTIAL,
            createdBy: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            allocations: [new ChargeAllocationDraft(Guid.NewGuid(), new Money(500m, "DOP"))]);

    [Fact]
    public void Clone_clears_identity_status_and_allocations()
    {
        var clone = ((ICloneable<ChargeDraft>)Original()).Clone();

        clone.Id.Should().BeNull();
        clone.Status.Should().Be(ChargeStatus.OPEN);
        clone.Allocations.Should().BeEmpty();
    }

    [Fact]
    public void Clone_copies_the_descriptive_fields()
    {
        var original = Original();
        var clone = ((ICloneable<ChargeDraft>)original).Clone();

        clone.CustomerId.Should().Be(original.CustomerId);
        clone.Code.Should().Be(original.Code);
        clone.Description.Should().Be(original.Description);
        clone.Amount.Should().Be(original.Amount);
        clone.IssuedOn.Should().Be(original.IssuedOn);
        clone.DueOn.Should().Be(original.DueOn);
        clone.CreatedBy.Should().Be(original.CreatedBy);
    }

    [Fact]
    public void Clone_shares_no_mutable_state_with_the_original()
    {
        var original = Original();
        var clone = ((ICloneable<ChargeDraft>)original).Clone();

        var reassigned = clone with
        {
            Amount = new Money(9999m, "DOP"),
            Description = "Otro",
            Allocations = [new ChargeAllocationDraft(Guid.NewGuid(), new Money(1m, "DOP"))],
        };

        reassigned.Amount.Amount.Should().Be(9999m);
        original.Amount.Amount.Should().Be(1500.50m);
        original.Description.Should().Be("Servicio de marzo");
        original.Allocations.Should().ContainSingle();
    }

    [Fact]
    public void Create_starts_as_an_open_draft_without_allocations()
    {
        var draft = ChargeDraft.Create(
            customerId: null,
            code: ChargeCode.Parse("FACT-002"),
            description: "Nuevo",
            amount: new Money(100m, "DOP"),
            issuedOn: new DateOnly(2026, 3, 1),
            dueOn: null,
            createdBy: null);

        draft.Id.Should().BeNull();
        draft.Status.Should().Be(ChargeStatus.OPEN);
        draft.Allocations.Should().BeEmpty();
    }
}
