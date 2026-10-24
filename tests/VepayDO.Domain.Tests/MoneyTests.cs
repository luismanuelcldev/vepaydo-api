using FluentAssertions;
using VepayDO.Domain;

namespace VepayDO.Domain.Tests;

public sealed partial class MoneyTests
{
    [Fact]
    public void Creates_money_from_number()
    {
        Money money = Money.FromNumber(100, "DOP");
        money.Amount.Should().Be(100m);
        money.Currency.Should().Be("DOP");
    }

    [Fact]
    public void Creates_from_string_with_decimal_precision()
    {
        Money money = Money.FromString("1500.50", "DOP");
        money.Amount.Should().Be(1500.50m);
    }

    [Fact]
    public void Rejects_empty_currency()
    {
        Action act = () => Money.FromNumber(100, string.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*Currency cannot be empty*");
    }

    [Fact]
    public void Adds_same_currency()
    {
        Money result = Money.FromNumber(100, "DOP").Add(Money.FromNumber(50, "DOP"));
        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("DOP");
    }

    [Fact]
    public void Rejects_adding_different_currencies()
    {
        Action act = () => Money.FromNumber(100, "DOP").Add(Money.FromNumber(50, "USD"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void Subtracts_same_currency()
    {
        Money result = Money.FromNumber(100, "DOP").Subtract(Money.FromNumber(40, "DOP"));
        result.Amount.Should().Be(60m);
    }

    [Fact]
    public void Rejects_subtracting_different_currencies()
    {
        Action act = () => Money.FromNumber(100, "DOP").Subtract(Money.FromNumber(40, "USD"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void Multiplies_by_factor()
    {
        Money money = Money.FromNumber(100, "DOP");
        money.Multiply(2.5m).Amount.Should().Be(250m);
    }

    [Fact]
    public void Divides_by_factor()
    {
        Money money = Money.FromNumber(100, "DOP");
        money.Divide(2).Amount.Should().Be(50m);
        money.Divide(4).Amount.Should().Be(25m);
    }

    [Fact]
    public void Rejects_divide_by_zero()
    {
        Action act = () => Money.FromNumber(100, "DOP").Divide(0);
        act.Should().Throw<DivideByZeroException>().WithMessage("*divide by zero*");
    }

    [Fact]
    public void Rounds_half_up_by_default_and_keeps_currency()
    {
        Money rounded = Money.FromString("123.456", "DOP").Round();
        rounded.Amount.Should().Be(123.46m);
        rounded.Currency.Should().Be("DOP");
    }
}
