namespace VepayDO.Infrastructure.Persistence;

/// <summary>
/// Accepts both conventions for a connection string: the URL form carried by
/// <c>DATABASE_URL</c> (the Prisma habit the reference project uses) and the
/// keyword form Npgsql speaks natively. Everything else is passed through
/// untouched so a hand-written <c>Host=...;Database=...</c> still works.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "postgresql" && uri.Scheme != "postgres"))
        {
            return value;
        }

        var builder = new List<string>(8);

        if (!string.IsNullOrEmpty(uri.Host))
        {
            builder.Add($"Host={uri.Host}");
        }

        if (!uri.IsDefaultPort && uri.Port > 0)
        {
            builder.Add($"Port={uri.Port}");
        }

        var database = uri.AbsolutePath.TrimStart('/');
        if (!string.IsNullOrEmpty(database))
        {
            builder.Add($"Database={Uri.UnescapeDataString(database)}");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            var user = separator < 0 ? uri.UserInfo : uri.UserInfo[..separator];
            var password = separator < 0 ? string.Empty : uri.UserInfo[(separator + 1)..];

            if (user.Length > 0)
            {
                builder.Add($"Username={Uri.UnescapeDataString(user)}");
            }

            if (password.Length > 0)
            {
                builder.Add($"Password={Uri.UnescapeDataString(password)}");
            }
        }

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var queryValue = Uri.UnescapeDataString(pair[(separator + 1)..]);

            builder.Add(key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                ? $"SSL Mode={TranslateSslMode(queryValue)}"
                : $"{key}={queryValue}");
        }

        return string.Join(';', builder);
    }

    private static string TranslateSslMode(string value) => value.ToLowerInvariant() switch
    {
        "disable" => "Disable",
        "allow" => "Prefer",
        "prefer" => "Prefer",
        "require" => "Require",
        "verify-ca" => "VerifyCA",
        "verify-full" => "VerifyFull",
        _ => value,
    };
}
