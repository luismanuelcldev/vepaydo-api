namespace VepayDO.Application.Security;

public sealed record AccessTokenClaims
{
    public required Guid UserId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? TenantId { get; init; }

    public string? Role { get; init; }

    public bool MustChangePassword { get; init; }
}

public sealed record IssuedRefreshToken(string Token, byte[] Hash);

public interface ITokenService
{
    string SignAccessToken(AccessTokenClaims claims);

    Task<AccessTokenClaims> VerifyAccessTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    IssuedRefreshToken IssueRefreshToken();

    byte[] HashRefreshToken(string token);

    bool HashesMatch(byte[] a, byte[] b);
}
