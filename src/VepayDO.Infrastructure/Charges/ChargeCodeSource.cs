using System.Security.Cryptography;

using VepayDO.Application.Charges.Ports;

namespace VepayDO.Infrastructure.Charges;

/// <summary>
/// Generates charge-code suffixes from a cryptographically strong source so codes
/// are not guessable and collisions are unlikely even before the tenant-unique
/// index backstop (AGENTS.md section 9.3).
/// </summary>
public sealed class ChargeCodeSource : IChargeCodeSource
{
    public int NextValue(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        return RandomNumberGenerator.GetInt32(maxExclusive);
    }
}
