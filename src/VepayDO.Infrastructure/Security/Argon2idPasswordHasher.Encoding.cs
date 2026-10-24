namespace VepayDO.Infrastructure.Security;

public sealed partial class Argon2idPasswordHasher
{
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
