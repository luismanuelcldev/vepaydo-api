using System.Security.Cryptography;
using System.Text;

namespace VepayDO.Infrastructure.Security;

public static class LicenseSentinel
{
    private const string ExpectedPayload = "VePayDO:Author:Luis Manuel De La Cruz Ledesma:Target:Production";

    private const string AuthorityPublicKeyXml =
        "<RSAKeyValue><Modulus>nRUa3GXAasJtC6fHNDSNknoyGqD4BThbENcm8y6qwoJ5rfTc+cuYiw5QzP10H6oHQOgMjZGfwq7s70pHncLJmiiuWTjU6OUAUDtDDxMk+YL1uVBSKa8XlksLcl0JQgG7mcn6PJf8qTO8L8Qgprxcpl8V3eRZsn/1RklOf7sob22Y+ZjO11aeKLvJly+RMpSRK0Y11Knor1/Y4FfK3A+7kSd3Op8IpYMPvpXx7XQDZAeYd6VVgcpvALLdZOb9mrbYxp94dM5HC5SsPQ0v01RU/7+Ua0yror7JjktbEGEaHFYov56myZ6dLPMFnEgT3BZOpH3fL+OLqRBzfMlBzpDvmQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    public static void AssertValid(string? environment, string? licenseSignature)
    {
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(licenseSignature))
        {
            throw new InvalidOperationException(
                "Execution halted: Missing VEPAYDO_LICENSE_KEY. " +
                "Production deployment requires an authorized digital signature issued by Luis Manuel De La Cruz Ledesma.");
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(AuthorityPublicKeyXml);

            var data = Encoding.UTF8.GetBytes(ExpectedPayload);
            var signature = Convert.FromBase64String(licenseSignature.Trim());

            if (!rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                throw new CryptographicException("License signature mismatch");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Execution halted: Invalid or tampered VEPAYDO_LICENSE_KEY. " +
                "Production execution is strictly protected by proprietary license of Luis Manuel De La Cruz Ledesma.",
                ex);
        }
    }
}
