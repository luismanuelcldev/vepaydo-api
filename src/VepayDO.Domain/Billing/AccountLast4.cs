using System.Text.RegularExpressions;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Billing;

/// <summary>
/// The last four digits of a business bank account (UC-SET-03, AGENTS.md section 7).
/// The app must never store or expose a full account number, so this value object
/// only ever carries the four digits and rejects anything else — mirroring the
/// <c>business_accounts_last4_format</c> CHECK (<c>^[0-9]{4}$</c>) so the invariant
/// lives in the domain too, not only in the database.
/// </summary>
public readonly record struct AccountLast4
{
    private static readonly Regex Pattern = new("^[0-9]{4}$", RegexOptions.Compiled);

    private AccountLast4(string value) => Value = value;

    /// <summary>The four digits, always present.</summary>
    public string Value { get; }

    /// <summary>Parses a caller-supplied value; throws when it is not exactly four digits.</summary>
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

    /// <summary>Masks any non-four-digit input so it can never be logged verbatim.</summary>
    public static string Mask(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : $"****{value[^Math.Min(4, value.Length)..]}";
}
