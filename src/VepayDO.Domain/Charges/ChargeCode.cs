using System.Globalization;

using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Charges;

/// <summary>
/// A charge code: a short, human-friendly identifier unique per tenant
/// (UC-DATA-03, AGENTS.md section 9.3). The stored form is uppercased and limited
/// to letters, digits and dashes so it stays easy to read aloud and type.
/// </summary>
public sealed record ChargeCode
{
    public const int MinLength = 1;

    public const int MaxLength = 24;

    private ChargeCode(string value)
    {
        Value = value;
    }

    /// <summary>The normalized, stored form (uppercase A–Z, 0–9 and dashes).</summary>
    public string Value { get; }

    /// <summary>
    /// Validates and normalizes a code. Trims, uppercases, collapses whitespace to
    /// a single dash and rejects anything outside <c>[A-Z0-9-]</c> or an empty result.
    /// </summary>
    public static ChargeCode Parse(string? value)
    {
        var upper = value?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalized = Collapse(upper);

        if (normalized.Length == 0)
        {
            throw new InvalidArgumentDomainException("Charge code is required");
        }

        if (normalized.Length > MaxLength)
        {
            throw new InvalidArgumentDomainException($"Charge code must be at most {MaxLength} characters");
        }

        foreach (var c in normalized)
        {
            if (!IsAllowed(c))
            {
                throw new InvalidArgumentDomainException("Charge code may only contain letters, digits and dashes");
            }
        }

        return new ChargeCode(normalized);
    }

    /// <summary>Generates a new code with the given prefix and a random suffix.</summary>
    public static ChargeCode Generate(string prefix, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        var cleanPrefix = Collapse((prefix ?? string.Empty).Trim().ToUpperInvariant());
        foreach (var c in cleanPrefix)
        {
            if (!IsAllowed(c))
            {
                cleanPrefix = "C";
                break;
            }
        }

        if (cleanPrefix.Length == 0)
        {
            cleanPrefix = "C";
        }

        if (cleanPrefix.Length > MaxLength - 7)
        {
            cleanPrefix = cleanPrefix[..(MaxLength - 7)];
        }

        var suffix = next(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        return new ChargeCode($"{cleanPrefix}-{suffix}");
    }

    public override string ToString() => Value;

    private static string Collapse(string value)
    {
        var chars = new List<char>(value.Length);
        var lastWasDash = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasDash && chars.Count > 0)
                {
                    chars.Add('-');
                    lastWasDash = true;
                }

                continue;
            }

            chars.Add(c);
            lastWasDash = c == '-';
        }

        return new string([.. chars]).Trim('-');
    }

    private static bool IsAllowed(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-';
}
