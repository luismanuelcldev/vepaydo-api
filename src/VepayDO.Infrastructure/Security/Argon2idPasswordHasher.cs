using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using VepayDO.Application.Security;

namespace VepayDO.Infrastructure.Security;

/// <summary>
/// argon2id password hashing in the self-describing PHC format
/// (<c>$argon2id$v=19$m=...,t=...,p=...$salt$hash</c>), the same wire format
/// produced by the reference <c>@node-rs/argon2</c> implementation, so a stored
/// hash carries its own parameters and can be upgraded later (AGENTS.md section 13).
/// Parameters follow the OWASP interactive-login baseline: m=19456 KiB, t=2, p=1.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    /// <summary>Salt bytes (128 bits).</summary>
    private const int SaltLength = 16;

    /// <summary>Derived key bytes (256 bits).</summary>
    private const int HashLength = 32;

    /// <summary>Memory cost in KiB.</summary>
    private const int MemorySizeKb = 19_456;

    private const int TimeCost = 2;

    private const int Parallelism = 1;

    /// <summary>Argon2 version 1.3 (<c>0x13</c>), encoded as <c>v=19</c>.</summary>
    private const int Argon2Version = 19;

    private const string Prefix = "$argon2id$";

    public string Hash(string plainPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainPassword);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var derived = Derive(plainPassword, salt);
        try
        {
            return Format(salt, derived);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derived);
        }
    }

    public bool Verify(string passwordHash, string plainPassword)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(plainPassword))
        {
            return false;
        }

        if (!TryParse(passwordHash, out var salt, out var expected, out var parameters))
        {
            return false;
        }

        byte[] derived;
        try
        {
            derived = Derive(
                plainPassword,
                salt,
                parameters.MemoryKb,
                parameters.TimeCost,
                parameters.Parallelism);
        }
        catch (Exception)
        {
            // A malformed or hostile hash must never surface as a server error.
            return false;
        }

        try
        {
            if (derived.Length != expected.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(derived, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derived);
            CryptographicOperations.ZeroMemory(salt);
        }
    }

    private static byte[] Derive(
        string plainPassword,
        byte[] salt,
        int memoryKb = MemorySizeKb,
        int timeCost = TimeCost,
        int parallelism = Parallelism)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(plainPassword);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = memoryKb,
                Iterations = timeCost,
                DegreeOfParallelism = parallelism,
            };

            return argon2.GetBytes(HashLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static string Format(byte[] salt, byte[] hash)
    {
        var builder = new StringBuilder(96);
        builder.Append(Prefix);
        builder.Append("v=").Append(Argon2Version).Append('$');
        builder.Append("m=").Append(MemorySizeKb);
        builder.Append(",t=").Append(TimeCost);
        builder.Append(",p=").Append(Parallelism);
        builder.Append('$');
        builder.Append(ToBase64NoPadding(salt));
        builder.Append('$');
        builder.Append(ToBase64NoPadding(hash));
        return builder.ToString();
    }

    private static bool TryParse(
        string encoded,
        out byte[] salt,
        out byte[] hash,
        out Argon2Parameters parameters)
    {
        salt = [];
        hash = [];
        parameters = default;

        if (!encoded.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        // $argon2id$ v=19 m=...,t=...,p=... salt hash  ->  6 segments when split on '$'.
        var segments = encoded.Split('$');
        if (segments.Length != 6 || segments[1] != "argon2id")
        {
            return false;
        }

        if (segments[2] is not { } versionSegment ||
            !versionSegment.StartsWith("v=", StringComparison.Ordinal) ||
            !int.TryParse(versionSegment[2..], out var version) ||
            version != Argon2Version)
        {
            return false;
        }

        if (!TryParseParameters(segments[3], out parameters))
        {
            return false;
        }

        try
        {
            salt = FromBase64NoPadding(segments[4]);
            hash = FromBase64NoPadding(segments[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length > 0 && hash.Length > 0;
    }

    private static bool TryParseParameters(string segment, out Argon2Parameters parameters)
    {
        parameters = default;

        int memoryKb = 0;
        int timeCost = 0;
        int parallelism = 0;
        var seenMemory = false;
        var seenTime = false;
        var seenParallelism = false;

        foreach (var pair in segment.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0 || separator == pair.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(pair[(separator + 1)..], out var value))
            {
                return false;
            }

            switch (pair[..separator])
            {
                case "m":
                    memoryKb = value;
                    seenMemory = true;
                    break;
                case "t":
                    timeCost = value;
                    seenTime = true;
                    break;
                case "p":
                    parallelism = value;
                    seenParallelism = true;
                    break;
                default:
                    return false;
            }
        }

        if (!seenMemory || !seenTime || !seenParallelism)
        {
            return false;
        }

        // Guard against a stored hash that would ask for absurd resources.
        if (memoryKb <= 0 || memoryKb > 1_048_576 || timeCost <= 0 || timeCost > 16 ||
            parallelism <= 0 || parallelism > 16)
        {
            return false;
        }

        parameters = new Argon2Parameters(memoryKb, timeCost, parallelism);
        return true;
    }

    private static string ToBase64NoPadding(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=');

    private static byte[] FromBase64NoPadding(string value)
    {
        var padded = (value.Length % 4) switch
        {
            0 => value,
            2 => value + "==",
            3 => value + "=",
            _ => throw new FormatException("Invalid base64url padding"),
        };

        return Convert.FromBase64String(padded);
    }

    private readonly record struct Argon2Parameters(int MemoryKb, int TimeCost, int Parallelism);
}
