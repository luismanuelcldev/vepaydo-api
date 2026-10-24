using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VepayDO.Application.Configuration;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;

namespace VepayDO.Infrastructure.Security;

public sealed partial class TokenService : ITokenService
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
            throw new InvalidArgumentDomainException($"{nameof(AppConfig.JwtAccessSecret)} must be at least 32 characters");
        if (string.IsNullOrEmpty(config.RefreshTokenPepper))
            throw new InvalidArgumentDomainException($"{nameof(AppConfig.RefreshTokenPepper)} is required");

        _clock = clock;
        _issuer = config.AppOrigin;
        _signingKey = Encoding.UTF8.GetBytes(config.JwtAccessSecret);
        _pepper = Encoding.UTF8.GetBytes(config.RefreshTokenPepper);
        _handler = new JsonWebTokenHandler { InboundClaimTypeMap = new Dictionary<string, string>() };
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

    public async Task<AccessTokenClaims> VerifyAccessTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new UnauthorizedException("Access token is missing");

        TokenValidationResult result;
        try { result = await _handler.ValidateTokenAsync(token, _validationParameters).ConfigureAwait(false); }
        catch (Exception) { throw new UnauthorizedException("Access token is invalid"); }

        if (!result.IsValid || result.SecurityToken is null)
            throw new UnauthorizedException(result.Exception?.Message ?? "Access token is invalid");

        return MapClaims(result.Claims);
    }
}
