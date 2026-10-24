using VepayDO.Domain.Errors;
using VepayDO.Domain.Text;

namespace VepayDO.Domain.Customers;

/// <summary>
/// A customer or alias display name (UC-DATA-01, AGENTS.md section 9.3). The
/// normalized form feeds the <c>SENDER</c> matching signal and the
/// <c>name_normalized</c>/<c>alias_normalized</c> columns, so both display and
/// normalized values are validated together and can never drift apart.
/// </summary>
public sealed record CustomerName
{
    public const int MaxLength = 160;

    private CustomerName(string display, string normalized)
    {
        Display = display;
        Normalized = normalized;
    }

    /// <summary>The trimmed, human-readable name as entered.</summary>
    public string Display { get; }

    /// <summary>The case-, accent- and punctuation-insensitive matching form.</summary>
    public string Normalized { get; }

    /// <summary>
    /// Validates and normalizes a name. Rejects an empty/whitespace name and one
    /// whose normalized form is empty (for example only punctuation).
    /// </summary>
    public static CustomerName Parse(string? value)
    {
        var display = value?.Trim() ?? string.Empty;

        if (display.Length == 0)
        {
            throw new InvalidArgumentDomainException("Name is required");
        }

        if (display.Length > MaxLength)
        {
            throw new InvalidArgumentDomainException($"Name must be at most {MaxLength} characters");
        }

        var normalized = TextNormalizer.NormalizeName(display);
        if (normalized.Length == 0)
        {
            throw new InvalidArgumentDomainException("Name must contain at least one letter or digit");
        }

        return new CustomerName(display, normalized);
    }
}
