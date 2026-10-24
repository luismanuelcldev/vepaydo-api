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
    public async Task Create_rejects_an_unsupported_currency()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Malo", 10m, "EUR", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Update_rejects_a_voided_charge()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001", status: "VOIDED"));

        var act = async () => await harness.Service.UpdateAsync(
            ChargeId,
            new UpdateChargeInput(null, null, "Nuevo", null, null, null, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Update_applies_only_the_supplied_fields()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var view = await harness.Service.UpdateAsync(
            ChargeId,
            new UpdateChargeInput(null, null, "Descripción nueva", 2500m, null, null, null),
            Meta(),
            CancellationToken.None);

        view.Description.Should().Be("Descripción nueva");
        view.Amount.Should().Be(2500m);
        view.Code.Should().Be("FACT-001");
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("update");
    }

    [Fact]
    public async Task Void_records_the_reason_and_audits()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var view = await harness.Service.VoidAsync(
            ChargeId,
            new VoidChargeInput("Creada por error"),
            Meta(),
            CancellationToken.None);

        view.Status.Should().Be("VOIDED");
        view.VoidReason.Should().Be("Creada por error");
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("void");
    }
}
