using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

/// <summary>
/// The two account gates: a one-time temporary password and pending legal
/// acceptance (AGENTS.md section 9.9, UC-AUTH-08).
/// </summary>
public sealed class AccountGateTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task TemporaryPassword_BlocksTenantRoutes()
    {
        factory.Legal.Accepted = true;
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER", mustChangePassword: true));

        var response = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status403Forbidden,
            "AUTH.PASSWORD_CHANGE_REQUIRED",
            "auth.password_change_required");
    }

    [Fact]
    public async Task TemporaryPassword_AllowsRoutesThatCanChangeIt()
    {
        factory.Legal.Accepted = true;
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER", mustChangePassword: true));

        var response = await client.GetAsync(new Uri("/api/v1/__test/password-change", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PendingLegalAcceptance_BlocksTenantRoutes()
    {
        factory.Legal.Accepted = false;
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri("/api/v1/__test/secure", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status403Forbidden,
            "LEGAL.ACCEPTANCE_REQUIRED",
            "legal.acceptance_required");
    }

    [Fact]
    public async Task PendingLegalAcceptance_AllowsRoutesThatCanAcceptIt()
    {
        factory.Legal.Accepted = false;
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri("/api/v1/__test/legal-allowed", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PendingLegalAcceptance_DoesNotAffectPublicRoutes()
    {
        factory.Legal.Accepted = false;
        var checksBefore = factory.Legal.CallCount;
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/__test/anonymous", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(checksBefore, factory.Legal.CallCount);
    }
}
