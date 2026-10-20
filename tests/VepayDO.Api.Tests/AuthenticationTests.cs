using VepayDO.Api.Tests.Fixtures;
using VepayDO.Application.Security;

namespace VepayDO.Api.Tests;

/// <summary>
/// Access tokens: a bare route requires one, a <c>[Public]</c> route does not,
/// and revoking the session carried by a token invalidates it immediately
/// (AGENTS.md sections 9.9 and 10).
/// </summary>
public sealed class AuthenticationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task SecureRoute_WithoutToken_Returns401ProblemDetails()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status401Unauthorized,
            "DOMAIN.UNAUTHORIZED",
            "error.unauthorized");
    }

    [Fact]
    public async Task SecureRoute_WithMalformedToken_Returns401()
    {
        var client = factory.CreateClientWithToken("not-a-jwt");

        var response = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecureRoute_WithTamperedToken_Returns401()
    {
        var token = factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER");
        var tampered = string.Concat(token.AsSpan(0, token.Length - 4), "AAAA");

        var client = factory.CreateClientWithToken(tampered);
        var response = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.NotEqual(token, tampered);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SecureRoute_WithValidToken_ReturnsOk()
    {
        var token = factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER");
        var client = factory.CreateClientWithToken(token);

        var response = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublicRoute_RequiresNoToken()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/__test/anonymous", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

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
