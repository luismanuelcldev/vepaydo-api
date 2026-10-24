using FluentAssertions;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Domain.Tests.Tenants;

public sealed partial class TenantSettingsValuesTests
{
    [Fact]
    public void Default_carries_the_documented_prototype_values()
    {
        var settings = TenantSettingsValues.Default;

        settings.DateWindowDays.Should().Be(7);
        settings.AmountTolerance.Should().Be(0m);
        settings.VerifierAmountCap.Should().BeNull();
        settings.ImageRetentionMonths.Should().Be(24);
    }

    [Fact]
    public void Clone_of_the_default_shares_no_mutable_state()
    {
        var clone = TenantSettingsValues.Default.FromPrototype();

        var mutated = clone with
        {
            DateWindowDays = 30,
            AmountTolerance = 5.5m,
            VerifierAmountCap = 1000m,
            ImageRetentionMonths = 3,
        };

        TenantSettingsValues.Default.DateWindowDays.Should().Be(7);
        TenantSettingsValues.Default.AmountTolerance.Should().Be(0m);
        TenantSettingsValues.Default.VerifierAmountCap.Should().BeNull();
        TenantSettingsValues.Default.ImageRetentionMonths.Should().Be(24);

        mutated.DateWindowDays.Should().Be(30);
        mutated.AmountTolerance.Should().Be(5.5m);
        mutated.VerifierAmountCap.Should().Be(1000m);
        mutated.ImageRetentionMonths.Should().Be(3);
    }

    [Fact]
    public void Two_clones_are_independent_of_each_other()
    {
        var first = TenantSettingsValues.Default.FromPrototype();
        var second = TenantSettingsValues.Default.FromPrototype();

        var adjusted = first with { DateWindowDays = 15 };

        second.DateWindowDays.Should().Be(7);
        adjusted.DateWindowDays.Should().Be(15);
    }

    [Fact]
    public void A_clone_is_equal_to_the_prototype_before_mutation()
    {
        TenantSettingsValues.Default.FromPrototype().Should().Be(TenantSettingsValues.Default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void Validate_rejects_a_date_window_out_of_range(short days)
    {
        var settings = TenantSettingsValues.Default.FromPrototype() with { DateWindowDays = days };

        var act = settings.Validate;

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Validate_rejects_a_negative_tolerance()
    {
        var settings = TenantSettingsValues.Default.FromPrototype() with { AmountTolerance = -0.01m };

        var act = settings.Validate;

        act.Should().Throw<InvalidArgumentDomainException>();
    }
}
