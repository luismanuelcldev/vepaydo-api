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
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Get_returns_the_stored_values()
    {
        var harness = new Harness();

        var view = await harness.Service.GetAsync(CancellationToken.None);

        view.DateWindowDays.Should().Be(7);
        view.AmountTolerance.Should().Be(0m);
        view.VerifierAmountCap.Should().BeNull();
        view.ImageRetentionMonths.Should().Be(24);
    }

    [Fact]
    public async Task Get_without_a_tenant_context_fails_closed()
    {
        var harness = new Harness(useNullContext: true);

        var act = async () => await harness.Service.GetAsync(CancellationToken.None);

        await act.Should().ThrowAsync<MissingTenantContextException>();
    }

    [Fact]
    public async Task Get_with_a_missing_settings_row_is_not_found()
    {
        var harness = new Harness { RowMissing = true };

        var act = async () => await harness.Service.GetAsync(CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_applies_a_partial_change_and_keeps_the_rest()
    {
        var harness = new Harness();

        var view = await harness.Service.UpdateAsync(
            new UpdateTenantSettingsInput(
                DateWindowDays: 30,
                AmountTolerance: null,
                VerifierAmountCap: null,
                ClearVerifierAmountCap: false,
                ImageRetentionMonths: null),
            Meta(),
            CancellationToken.None);

        view.DateWindowDays.Should().Be(30);
        view.AmountTolerance.Should().Be(0m);
        view.VerifierAmountCap.Should().BeNull();
        view.ImageRetentionMonths.Should().Be(24);
        harness.Settings.Stored!.DateWindowDays.Should().Be(30);
    }
}
