using VepayDO.Domain.Errors;
using VepayDO.Domain.Text;

namespace VepayDO.Domain.Customers;

public sealed record CustomerName
{
    public const int MaxLength = 160;

    private CustomerName(string display, string normalized)
    {
        Display = display;
        Normalized = normalized;
    }

    public string Display { get; }

    public string Normalized { get; }

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
