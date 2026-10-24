using System.Globalization;
using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Charges;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Application.Tests.Charges;

public sealed partial class ChargeServiceTests
{

    [Fact]
    public async Task Void_rejects_an_already_voided_charge()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001", status: "VOIDED"));

        var act = async () => await harness.Service.VoidAsync(
            ChargeId,
            new VoidChargeInput("otra"),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Void_requires_a_reason()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var act = async () => await harness.Service.VoidAsync(
            ChargeId,
            new VoidChargeInput("   "),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Duplicate_creates_an_open_copy_with_a_fresh_code()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001", status: "PARTIAL", applied: 500m));

        var view = await harness.Service.DuplicateAsync(
            ChargeId,
            new DuplicateChargeInput(null),
            Meta(),
            CancellationToken.None);

        view.Id.Should().NotBe(ChargeId);
        view.Status.Should().Be("OPEN");
        view.AmountApplied.Should().Be(0m);
        view.Code.Should().Be("SER-000042");
        view.Amount.Should().Be(1500m);
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("duplicate");
    }
}
