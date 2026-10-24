using FluentAssertions;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Domain.Tests.Tenants;

public sealed partial class TenantSettingsValuesTests
{

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_rejects_a_non_positive_cap(decimal cap)
    {
        var settings = TenantSettingsValues.Default.FromPrototype() with { VerifierAmountCap = cap };

        var act = settings.Validate;

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Validate_accepts_a_null_cap()
    {
        var settings = TenantSettingsValues.Default.FromPrototype();

        var act = settings.Validate;

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Validate_rejects_a_retention_out_of_range(short months)
    {
        var settings = TenantSettingsValues.Default.FromPrototype() with { ImageRetentionMonths = months };

        var act = settings.Validate;

        act.Should().Throw<InvalidArgumentDomainException>();
    }
}
