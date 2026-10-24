using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed class AuthorizationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Owner_IsAllowed_OnOwnerOnlyRoute()
    {
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri("/api/v1/__test/owner-only", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_IsForbidden_OnOwnerOnlyRoute()
    {
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/__test/owner-only", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status403Forbidden,
            "DOMAIN.FORBIDDEN",
            "error.forbidden");
    }

    [Fact]
    public async Task Verifier_IsAllowed_OnReviewRoute()
    {
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/__test/verifier-allowed", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenWithoutTenant_IsForbidden_OnPermissionRoute()
    {

        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId));

        var response = await client.GetAsync(new Uri("/api/v1/__test/owner-only", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status403Forbidden,
            "DOMAIN.FORBIDDEN",
            "error.forbidden");
    }
}
