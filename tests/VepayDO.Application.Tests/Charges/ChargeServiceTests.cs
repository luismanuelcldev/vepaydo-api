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
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ChargeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CustomerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Create_generates_a_code_when_none_is_supplied()
    {
        var harness = new Harness();

        var view = await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Servicio de marzo", 1500m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        view.Code.Should().Be("SER-000042");
        view.Status.Should().Be("OPEN");
        view.Balance.Should().Be(1500m);
        view.AmountApplied.Should().Be(0m);
        harness.Audit.Events.Should().ContainSingle();
        harness.Audit.Events[0].Action.Should().Be("create");
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_explicit_code()
    {
        var harness = new Harness();
        harness.Repo.Seed(Sample(ChargeId, "FACT-001"));

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, "fact-001", "Otro", 10m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_rejects_a_non_positive_amount()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Malo", 0m, "DOP", new DateOnly(2026, 3, 1), null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Create_rejects_a_due_date_before_the_issue_date()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.CreateAsync(
            new CreateChargeInput(null, null, "Malo", 10m, "DOP", new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 1)),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }
}
