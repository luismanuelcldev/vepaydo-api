using FluentAssertions;
using VepayDO.Domain;

namespace VepayDO.Domain.Tests;

public sealed class MoneyTests
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
    public void Subtracts_amounts()
    {
        Money result = Money.FromNumber(100, "DOP").Subtract(Money.FromNumber(30, "DOP"));

        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void Rejects_subtracting_different_currencies()
    {
        Action act = () => Money.FromNumber(100, "DOP").Subtract(Money.FromNumber(30, "USD"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*different currencies*");
    }

    [Fact]
    public void Multiplies_by_factor()
    {
        Money money = Money.FromNumber(100, "DOP");

        money.Multiply(2).Amount.Should().Be(200m);
        money.Multiply(0.5m).Amount.Should().Be(50m);
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

    [Theory]
    [InlineData("10.555", 2, MidpointRounding.AwayFromZero, 10.56)]
    [InlineData("10.554", 2, MidpointRounding.ToZero, 10.55)]
    [InlineData("2.225", 2, MidpointRounding.ToEven, 2.22)]
    [InlineData("10.5", 0, MidpointRounding.AwayFromZero, 11)]
    public void Rounds_with_specified_mode(string input, int places, MidpointRounding mode, decimal expected)
    {
        Money.FromString(input, "DOP").Round(places, mode).Amount.Should().Be(expected);
    }

    [Fact]
    public void Compares_amounts()
    {
        Money a = Money.FromNumber(100, "DOP");
        Money b = Money.FromNumber(50, "DOP");

        a.GreaterThan(b).Should().BeTrue();
        b.LessThan(a).Should().BeTrue();
        a.GreaterThanOrEqualTo(a).Should().BeTrue();
        a.LessThanOrEqualTo(a).Should().BeTrue();
    }

    [Fact]
    public void Rejects_comparing_different_currencies()
    {
        Money a = Money.FromNumber(100, "DOP");
        Money b = Money.FromNumber(50, "USD");

        a.Invoking(x => x.GreaterThan(b)).Should().Throw<InvalidOperationException>();
        a.Invoking(x => x.LessThan(b)).Should().Throw<InvalidOperationException>();
        a.Invoking(x => x.GreaterThanOrEqualTo(b)).Should().Throw<InvalidOperationException>();
        a.Invoking(x => x.LessThanOrEqualTo(b)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Checks_equality_without_throwing_on_cross_currency()
    {
        Money a = Money.FromNumber(100, "DOP");

        a.Equals(Money.FromNumber(100, "DOP")).Should().BeTrue();
        a.Equals(Money.FromNumber(100, "USD")).Should().BeFalse();
    }

    [Fact]
    public void Checks_is_zero()
    {
        Money.FromNumber(0, "DOP").IsZero.Should().BeTrue();
        Money.FromNumber(5, "DOP").IsZero.Should().BeFalse();
    }

    [Fact]
    public void Keeps_decimal_precision_on_division()
    {
        Money result = Money.FromString("10", "DOP").Divide(3);

        result.Amount.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be("3.333333");
    }

    [Fact]
    public void Does_not_lose_precision_on_classic_float_traps()
    {
        Money result = Money.FromString("0.1", "DOP").Add(Money.FromString("0.2", "DOP"));

        result.Amount.Should().Be(0.3m);
    }
}
