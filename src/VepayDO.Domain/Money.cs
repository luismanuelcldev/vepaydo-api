using System.Globalization;

namespace VepayDO.Domain;

public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) throw new ArgumentException("Currency cannot be empty", nameof(currency));
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }
    public string Currency { get; }

    public static Money FromDecimal(decimal amount, string currency) => new(amount, currency);
    public static Money FromNumber(double amount, string currency) => new((decimal)amount, currency);
    public static Money FromString(string amount, string currency) =>
        new(decimal.Parse(amount, CultureInfo.InvariantCulture), currency);

    public Money Add(Money other) { EnsureSameCurrency(other, "add"); return new Money(Amount + other.Amount, Currency); }
    public Money Subtract(Money other) { EnsureSameCurrency(other, "subtract"); return new Money(Amount - other.Amount, Currency); }
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);
    public Money Divide(decimal factor) => factor == 0m ? throw new DivideByZeroException("Cannot divide by zero") : new Money(Amount / factor, Currency);
    public Money Round(int decimalPlaces = 2, MidpointRounding mode = MidpointRounding.AwayFromZero) =>
        new(decimal.Round(Amount, decimalPlaces, mode), Currency);

    public bool IsZero => Amount == 0m;
    public bool GreaterThan(Money other) { EnsureSameCurrency(other, "compare"); return Amount > other.Amount; }
    public bool LessThan(Money other) { EnsureSameCurrency(other, "compare"); return Amount < other.Amount; }
    public bool GreaterThanOrEqualTo(Money other) { EnsureSameCurrency(other, "compare"); return Amount >= other.Amount; }
    public bool LessThanOrEqualTo(Money other) { EnsureSameCurrency(other, "compare"); return Amount <= other.Amount; }

    public bool Equals(Money other) => Currency == other.Currency && Amount == other.Amount;
    public override bool Equals(object? obj) => obj is Money other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(other, "compare");
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Currency} {Amount:0.00}");

    private void EnsureSameCurrency(Money other, string operation)
    {
        if (Currency != other.Currency) throw new InvalidOperationException($"Cannot {operation} amounts with different currencies");
    }
}
