using FluentAssertions;
using VepayDO.Domain.Customers;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tests.Customers;

/// <summary>
/// Pins the name normalization of <see cref="CustomerName"/> (UC-DATA-01, AGENTS.md
/// section 9.3): display and normalized forms are produced together and share the
/// same validation, so the <c>SENDER</c> signal and the stored columns never drift.
/// </summary>
public sealed class CustomerNameTests
{
    [Fact]
    public void Parse_keeps_the_display_form_and_produces_the_normalized_form()
    {
        var name = CustomerName.Parse("  José  PÉREZ-María  ");

        name.Display.Should().Be("José  PÉREZ-María");
        name.Normalized.Should().Be("jose perez maria");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("---")]
    public void Parse_rejects_names_without_letters_or_digits(string value)
    {
        var act = () => CustomerName.Parse(value);

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Parse_rejects_a_name_longer_than_the_limit()
    {
        var value = new string('a', CustomerName.MaxLength + 1);

        var act = () => CustomerName.Parse(value);

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Two_spellings_of_the_same_name_normalize_identically()
    {
        CustomerName.Parse("María José").Normalized
            .Should().Be(CustomerName.Parse("MARIA JOSE").Normalized);
    }
}
