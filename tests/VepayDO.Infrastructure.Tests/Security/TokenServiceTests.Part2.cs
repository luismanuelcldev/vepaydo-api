using System.Text;
using FluentAssertions;
using VepayDO.Application.Configuration;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Security;

namespace VepayDO.Infrastructure.Tests.Security;

public sealed partial class TokenServiceTests
{

    [Fact]
    public async Task Rejects_a_tampered_token()
    {
        var service = Service();
        var token = service.SignAccessToken(new AccessTokenClaims
        {
            UserId = Guid.NewGuid(),
            Role = "VERIFIER",
        });

        var parts = token.Split('.');
        var tampered = parts[0] + "." + parts[1] + "x." + parts[2];

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => service.VerifyAccessTokenAsync(tampered));
    }

    [Fact]
    public async Task Rejects_a_token_signed_with_a_different_secret()
    {
        var signing = new TokenService(
            Config() with { JwtAccessSecret = new string('z', 32) },
            new SystemClock());
        var token = signing.SignAccessToken(new AccessTokenClaims { UserId = Guid.NewGuid() });

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().VerifyAccessTokenAsync(token));
    }

    [Fact]
    public async Task Rejects_a_token_issued_for_a_different_origin()
    {
        var token = Service("https://app.vepaydo.example")
            .SignAccessToken(new AccessTokenClaims { UserId = Guid.NewGuid() });

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service("https://staging.vepaydo.example").VerifyAccessTokenAsync(token));
    }

    [Fact]
    public async Task Rejects_an_expired_token()
    {
        var stale = new TokenService(Config(), FixedClock.At("2020-01-01T00:00:00Z"));
        var token = stale.SignAccessToken(new AccessTokenClaims { UserId = Guid.NewGuid() });

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().VerifyAccessTokenAsync(token));
    }

    [Fact]
    public async Task Rejects_an_empty_token()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Service().VerifyAccessTokenAsync(string.Empty));
    }

    [Fact]
    public void Refuses_to_build_without_a_usable_secret()
    {
        TokenService? created = null;
        Action shortSecret = () => created = new TokenService(
            Config() with { JwtAccessSecret = "too-short" },
            new SystemClock());

        shortSecret.Should().Throw<InvalidArgumentDomainException>();
        created.Should().BeNull();
    }
}
