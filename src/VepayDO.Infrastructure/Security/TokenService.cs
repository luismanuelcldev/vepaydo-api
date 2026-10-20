using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VepayDO.Application.Configuration;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Infrastructure.Security;

/// <summary>
/// Issues the 15-minute HS256 access JWT and the opaque rotating refresh token
/// (AGENTS.md section 13). The refresh token is generated with 256 bits of
/// entropy and only <c>SHA-256(pepper + token)</c> is ever persisted.
/// Issuer and audience are the configured <c>AppOrigin</c> so a token minted for
/// one deployment is rejected by every other.
/// </summary>
public sealed class TokenService : ITokenService
{
    private const string HmacSha256 = SecurityAlgorithms.HmacSha256;

    private readonly IClock _clock;
    private readonly string _issuer;
    private readonly byte[] _signingKey;
    private readonly byte[] _pepper;
    private readonly JsonWebTokenHandler _handler;
    private readonly TokenValidationParameters _validationParameters;

    public TokenService(AppConfig config, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        if (string.IsNullOrEmpty(config.JwtAccessSecret) || config.JwtAccessSecret.Length < 32)
        {
            throw new InvalidArgumentDomainException(
                $"{nameof(AppConfig.JwtAccessSecret)} must be at least 32 characters");
        }

        if (string.IsNullOrEmpty(config.RefreshTokenPepper))
        {
            throw new InvalidArgumentDomainException(
                $"{nameof(AppConfig.RefreshTokenPepper)} is required");
        }

        _clock = clock;
        _issuer = config.AppOrigin;
        _signingKey = Encoding.UTF8.GetBytes(config.JwtAccessSecret);
        _pepper = Encoding.UTF8.GetBytes(config.RefreshTokenPepper);

        _handler = new JsonWebTokenHandler
        {
            // Claim names stay exactly as written in the token ('sub', 'tid', ...).
            InboundClaimTypeMap = new Dictionary<string, string>(),
        };

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_signingKey),
            ValidAlgorithms = [HmacSha256],

            ValidateIssuer = true,
            ValidIssuer = _issuer,

            ValidateAudience = true,
            ValidAudience = _issuer,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public string SignAccessToken(AccessTokenClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var now = _clock.UtcNow.UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(BuildClaims(claims)),
            Issuer = _issuer,
            Audience = _issuer,
            NotBefore = now,
            IssuedAt = now,
            Expires = now.Add(TokenConstants.AccessTokenLifetime),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_signingKey), HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }

    public async Task<AccessTokenClaims> VerifyAccessTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new UnauthorizedException("Access token is missing");
        }

        TokenValidationResult result;
        try
        {
            result = await _handler.ValidateTokenAsync(token, _validationParameters)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A malformed token must read as "unauthorized", never as a server error.
            throw new UnauthorizedException("Access token is invalid");
        }

        if (!result.IsValid || result.SecurityToken is null)
        {
            throw new UnauthorizedException(
                result.Exception?.Message ?? "Access token is invalid");
        }

        return MapClaims(result.Claims);
    }

    public IssuedRefreshToken IssueRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return new IssuedRefreshToken(token, HashRefreshToken(token));
    }

    public byte[] HashRefreshToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        // SHA-256(pepper || token); the pepper keeps a stolen database useless.
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

    private static IEnumerable<Claim> BuildClaims(AccessTokenClaims claims)
    {
        yield return new Claim(TokenConstants.SubjectClaim, claims.UserId.ToString("D"));
        yield return new Claim(TokenConstants.TypeClaim, TokenConstants.TokenTypeAccess);

        if (claims.SessionId is { } sessionId)
        {
            yield return new Claim(TokenConstants.SessionIdClaim, sessionId.ToString("D"));
        }

        if (claims.TenantId is { } tenantId)
        {
            yield return new Claim(TokenConstants.TenantIdClaim, tenantId.ToString("D"));
        }

        if (!string.IsNullOrEmpty(claims.Role))
        {
            yield return new Claim(TokenConstants.RoleClaim, claims.Role);
        }

        if (claims.MustChangePassword)
        {
            yield return new Claim(TokenConstants.PasswordChangeClaim, TokenConstants.PasswordChangeTrue);
        }
    }

    private static AccessTokenClaims MapClaims(IDictionary<string, object> claims)
    {
        if (!TryReadGuid(claims, TokenConstants.SubjectClaim, out var userId) ||
            userId == Guid.Empty)
        {
            throw new UnauthorizedException("Access token has no subject");
        }

        var sessionId = TryReadGuid(claims, TokenConstants.SessionIdClaim, out var parsedSession)
            ? parsedSession
            : (Guid?)null;
        var tenantId = TryReadGuid(claims, TokenConstants.TenantIdClaim, out var parsedTenant)
            ? parsedTenant
            : (Guid?)null;

        var role = claims.TryGetValue(TokenConstants.RoleClaim, out var roleValue)
            ? roleValue?.ToString()
            : null;

        var mustChangePassword = claims.TryGetValue(TokenConstants.PasswordChangeClaim, out var pwd) &&
            string.Equals(pwd?.ToString(), "true", StringComparison.OrdinalIgnoreCase);

        return new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            TenantId = tenantId,
            Role = role,
            MustChangePassword = mustChangePassword,
        };
    }

    private static bool TryReadGuid(
        IDictionary<string, object> claims,
        string claimType,
        out Guid value)
    {
        value = Guid.Empty;
        if (!claims.TryGetValue(claimType, out var raw))
        {
            return false;
        }

        return Guid.TryParse(raw?.ToString(), out value);
    }
}
