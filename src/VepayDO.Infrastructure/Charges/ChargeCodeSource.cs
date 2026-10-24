using System.Security.Cryptography;

using VepayDO.Application.Charges.Ports;

namespace VepayDO.Infrastructure.Charges;

public sealed class ChargeCodeSource : IChargeCodeSource
{
    public int NextValue(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);

        return RandomNumberGenerator.GetInt32(maxExclusive);
    }
}
