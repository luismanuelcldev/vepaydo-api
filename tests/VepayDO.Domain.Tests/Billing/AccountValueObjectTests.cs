using FluentAssertions;
using VepayDO.Domain.Billing;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tests.Billing;

/// <summary>
/// Pins the invariants of the business-account value objects (UC-SET-03): the
/// account number is only ever the last four digits, and the currency matches the
/// database CHECK so the app never stores an unsupported code.
/// </summary>
public sealed class AccountValueObjectTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("0000")]
    public void Last4_accepts_exactly_four_digits(string value)
    {
        var last4 = AccountLast4.Parse(value);

        last4.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    [InlineData("")]
    [InlineData(null)]
    public void Last4_rejects_anything_but_four_digits(string? value)
    {
        var act = () => AccountLast4.Parse(value);

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Last4_trims_surrounding_whitespace()
    {
        AccountLast4.Parse("  9876  ").Value.Should().Be("9876");
    }

    [Fact]
    public void Last4_mask_never_reveals_more_than_four_digits()
    {
        AccountLast4.Mask("1234567890").Should().Be("****7890");
        AccountLast4.Mask("1234").Should().Be("****1234");
        AccountLast4.Mask("").Should().BeEmpty();
    }

    [Theory]
    [InlineData("DOP")]
    [InlineData("dop")]
    public void Currencies_accepts_supported_codes_case_insensitively(string code)
    {
        Currencies.Require(code).Should().Be(code.ToUpperInvariant());
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("")]
    [InlineData(null)]
    public void Currencies_rejects_unsupported_codes(string? code)
    {
        var act = () => Currencies.Require(code);

        act.Should().Throw<InvalidArgumentDomainException>();
    }
}
