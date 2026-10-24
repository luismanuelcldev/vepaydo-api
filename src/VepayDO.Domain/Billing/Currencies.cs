namespace VepayDO.Domain.Billing;

/// <summary>
/// The currencies the schema allows on business accounts and charges
/// (<c>CHAR(3)</c> with a CHECK on <c>'DOP'</c>/<c>'USD'</c>, AGENTS.md section 9.2).
/// Kept as one source of truth so the domain, the API validators and the tests
/// never drift from the database constraint.
/// </summary>
public static class Currencies
{
    public const string Dop = "DOP";

    public const string Usd = "USD";

    public static readonly IReadOnlyList<string> All = [Dop, Usd];

    public static bool IsSupported(string? currency) =>
        currency is Dop or Usd;

    /// <summary>Uppercases and validates; throws when the code is not supported.</summary>
    public static string Require(string? currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();

        if (!IsSupported(normalized))
        {
            throw new Errors.InvalidArgumentDomainException(
                $"Currency must be one of {string.Join(", ", All)}");
        }

        return normalized!;
    }
}
