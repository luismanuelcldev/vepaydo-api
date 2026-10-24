using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VepayDO.Application.Security;

namespace VepayDO.Infrastructure.Security;

public sealed partial class TokenService
{
    public IssuedRefreshToken IssueRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new IssuedRefreshToken(token, HashRefreshToken(token));
    }

    public byte[] HashRefreshToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var peppered = new byte[_pepper.Length + tokenBytes.Length];
        try
        {
            Buffer.BlockCopy(_pepper, 0, peppered, 0, _pepper.Length);
            Buffer.BlockCopy(tokenBytes, 0, peppered, _pepper.Length, tokenBytes.Length);
            return SHA256.HashData(peppered);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(peppered);
            CryptographicOperations.ZeroMemory(tokenBytes);
        }
    }

    public bool HashesMatch(byte[] a, byte[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
