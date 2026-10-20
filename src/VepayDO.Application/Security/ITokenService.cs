namespace VepayDO.Application.Security;

/// <summary>
/// Claims carried by the 15-minute access token. Mirrors the reference
/// <c>AccessTokenClaims</c>; everything needed by the HTTP guards lives here so
/// no database read is required to authorize a request.
/// </summary>
public sealed record AccessTokenClaims
{
    public required Guid UserId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? TenantId { get; init; }

    public string? Role { get; init; }

    public bool MustChangePassword { get; init; }
}

/// <summary>An opaque refresh token together with the hash persisted in <c>sessions.token_hash</c>.</summary>
public sealed record IssuedRefreshToken(string Token, byte[] Hash);

/// <summary>
/// Issues and hashes the two token kinds. The refresh token is never stored in
/// clear text: only <c>SHA-256(pepper + token)</c> reaches the database.
/// </summary>
public interface ITokenService
{
    string SignAccessToken(AccessTokenClaims claims);

    /// <summary>
    /// Parses and validates an access token. Throws on a bad signature,
    /// an expired token, or a wrong issuer/audience.
    /// </summary>
    Task<AccessTokenClaims> VerifyAccessTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    IssuedRefreshToken IssueRefreshToken();

    byte[] HashRefreshToken(string token);

    /// <summary>Constant-time comparison for hashes of the same length.</summary>
    bool HashesMatch(byte[] a, byte[] b);
}
