using System.Text.RegularExpressions;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Billing;

public readonly record struct AccountLast4
{
    private static readonly Regex Pattern = new("^[0-9]{4}$", RegexOptions.Compiled);

    private AccountLast4(string value) => Value = value;

    public string Value { get; }

    public static AccountLast4 Parse(string? value)
    {
        var trimmed = value?.Trim();

        if (trimmed is null || !Pattern.IsMatch(trimmed))
        {
            throw new InvalidArgumentDomainException("Last4 must be exactly four digits");
        }

        return new AccountLast4(trimmed);
    }

    public override string ToString() => Value;

    public static string Mask(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $"****{value[^Math.Min(4, value.Length)..]}";
}
