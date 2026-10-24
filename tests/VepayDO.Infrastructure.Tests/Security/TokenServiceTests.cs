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
}
