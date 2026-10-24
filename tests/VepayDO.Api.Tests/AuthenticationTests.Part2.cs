using VepayDO.Api.Tests.Fixtures;
using VepayDO.Application.Security;

namespace VepayDO.Api.Tests;

public sealed partial class AuthenticationTests
{

    [Fact]
    public async Task SecureRoute_WithRevokedSession_Returns401()
    {
        var sessionId = Guid.NewGuid();
        var token = factory.CreateAccessToken(
            TestData.OwnerUserId,
            TestData.TenantId,
            "OWNER",
            sessionId);

        var client = factory.CreateClientWithToken(token);
        var before = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await factory.Services.GetRequiredService<ISessionRevocationStore>().RevokeAsync(sessionId);

        var after = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        var problem = await ProblemReader.ReadAsync(after);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status401Unauthorized,
            "DOMAIN.UNAUTHORIZED",
            "error.unauthorized");
    }
}
