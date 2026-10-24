namespace VepayDO.Domain.Billing;

public static class Currencies
{
    public const string Dop = "DOP";

    public const string Usd = "USD";

    public static readonly IReadOnlyList<string> All = [Dop, Usd];

    public static bool IsSupported(string? currency) =>
        currency is Dop or Usd;

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
