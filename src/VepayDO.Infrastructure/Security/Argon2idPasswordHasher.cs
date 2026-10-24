using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using VepayDO.Application.Security;

namespace VepayDO.Infrastructure.Security;

public sealed partial class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int MemorySizeKb = 19_456;
    private const int TimeCost = 2;
    private const int Parallelism = 1;
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
            derived = Derive(plainPassword, salt, parameters.MemoryKb, parameters.TimeCost, parameters.Parallelism);
        }
        catch (Exception)
        {
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
}
