using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tests.Tenants;

public sealed partial class TenantSettingsServiceTests
{

    [Fact]
    public async Task Update_can_clear_the_verifier_cap()
    {
        var harness = new Harness();
        harness.Settings.Stored = TenantSettingsValues.Default.FromPrototype() with { VerifierAmountCap = 5000m };

        var view = await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, null, null, ClearVerifierAmountCap: true, null),
            Meta(),
            CancellationToken.None);

        view.VerifierAmountCap.Should().BeNull();
    }

    [Fact]
    public async Task Update_can_set_the_verifier_cap()
    {
        var harness = new Harness();

        var view = await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, null, 2500m, ClearVerifierAmountCap: false, null),
            Meta(),
            CancellationToken.None);

        view.VerifierAmountCap.Should().Be(2500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Update_rejects_an_out_of_range_window(short days)
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(days, null, null, false, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
        harness.Audit.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_rejects_a_negative_tolerance()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, -1m, null, false, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }

    [Fact]
    public async Task Update_rejects_a_non_positive_cap()
    {
        var harness = new Harness();

        var act = async () => await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(null, null, 0m, false, null),
            Meta(),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidArgumentDomainException>();
    }
}
