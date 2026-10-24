using System.Text;

namespace VepayDO.Infrastructure.Security;

public sealed partial class Argon2idPasswordHasher
{
    private static string Format(byte[] salt, byte[] hash)
    {
        var builder = new StringBuilder(96);
        builder.Append(Prefix).Append("v=").Append(Argon2Version).Append('$');
        builder.Append("m=").Append(MemorySizeKb).Append(",t=").Append(TimeCost).Append(",p=").Append(Parallelism).Append('$');
        builder.Append(ToBase64NoPadding(salt)).Append('$').Append(ToBase64NoPadding(hash));
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

        if (!encoded.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var segments = encoded.Split('$');
        if (segments.Length != 6 || segments[1] != "argon2id") return false;

        if (segments[2] is not { } versionSegment ||
            !versionSegment.StartsWith("v=", StringComparison.Ordinal) ||
            !int.TryParse(versionSegment[2..], out var version) ||
            version != Argon2Version)
        {
            return false;
        }

        if (!TryParseParameters(segments[3], out parameters)) return false;

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
        int memoryKb = 0, timeCost = 0, parallelism = 0;
        bool seenMemory = false, seenTime = false, seenParallelism = false;

        foreach (var pair in segment.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0 || separator == pair.Length - 1) return false;
            if (!int.TryParse(pair[(separator + 1)..], out var value)) return false;

            switch (pair[..separator])
            {
                case "m": memoryKb = value; seenMemory = true; break;
                case "t": timeCost = value; seenTime = true; break;
                case "p": parallelism = value; seenParallelism = true; break;
                default: return false;
            }
        }

        if (!seenMemory || !seenTime || !seenParallelism) return false;
        if (memoryKb <= 0 || memoryKb > 1_048_576 || timeCost <= 0 || timeCost > 16 || parallelism <= 0 || parallelism > 16) return false;

        parameters = new Argon2Parameters(memoryKb, timeCost, parallelism);
        return true;
    }
}
