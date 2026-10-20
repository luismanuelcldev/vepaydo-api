using System.Text;
using FluentAssertions;
using VepayDO.Application.Configuration;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Security;

namespace VepayDO.Infrastructure.Tests.Security;

public sealed class TokenServiceTests
{
    private static AppConfig Config(string origin = "https://app.vepaydo.example") => new()
    {
        DatabaseUrl = "postgresql://user:pass@localhost:5432/vepaydo",
        JwtAccessSecret = new string('a', 32),
        RefreshTokenPepper = new string('b', 32),
        AppOrigin = origin,
    };

    private static TokenService Service(string origin = "https://app.vepaydo.example") =>
        new(Config(origin), new SystemClock());

    [Fact]
    public async Task Round_trips_every_access_token_claim()
    {
        var service = Service();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var token = service.SignAccessToken(new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            TenantId = tenantId,
            Role = "OWNER",
            MustChangePassword = true,
        });

        var verified = await service.VerifyAccessTokenAsync(token);

        verified.UserId.Should().Be(userId);
        verified.SessionId.Should().Be(sessionId);
        verified.TenantId.Should().Be(tenantId);
        verified.Role.Should().Be("OWNER");
        verified.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Omits_optional_claims_that_are_not_set()
    {
        var service = Service();

        var token = service.SignAccessToken(new AccessTokenClaims { UserId = Guid.NewGuid() });

        var verified = await service.VerifyAccessTokenAsync(token);

        verified.SessionId.Should().BeNull();
        verified.TenantId.Should().BeNull();
        verified.Role.Should().BeNull();
        verified.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void Marks_the_token_as_an_access_token()
    {
        var token = Service().SignAccessToken(new AccessTokenClaims { UserId = Guid.NewGuid() });

        var json = DecodeSegment(token.Split('.')[1]);

        json.Should().Contain("\"typ\":\"access\"");
    }

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

    [Fact]
    public void Refresh_tokens_are_opaque_and_base64url()
    {
        var issued = Service().IssueRefreshToken();

        issued.Token.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        issued.Hash.Should().HaveCount(32);
    }

    [Fact]
    public void Refresh_token_hash_is_deterministic_and_depends_on_the_pepper()
    {
        var service = Service();
        var issued = service.IssueRefreshToken();

        service.HashRefreshToken(issued.Token).Should().BeEquivalentTo(issued.Hash);

        var peppered = new TokenService(
            Config() with { RefreshTokenPepper = new string('c', 32) },
            new SystemClock());

        peppered.HashRefreshToken(issued.Token).Should().NotBeEquivalentTo(issued.Hash);
    }

    [Fact]
    public void Hash_comparison_rejects_different_lengths_and_differs_byte_wise()
    {
        var service = Service();
        var a = service.IssueRefreshToken().Hash;
        var b = service.IssueRefreshToken().Hash;

        service.HashesMatch(a, a).Should().BeTrue();
        service.HashesMatch(a, b).Should().BeFalse();
        service.HashesMatch(a, [1, 2, 3]).Should().BeFalse();
    }

    private static string DecodeSegment(string base64url)
    {
        var base64 = base64url.Replace('-', '+').Replace('_', '/');
        var padded = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("Invalid base64url length"),
        };

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
