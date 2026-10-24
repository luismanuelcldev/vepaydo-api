using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed partial class OriginGuardTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory factory = factory;

    private const string ForeignOrigin = "https://evil.example";

    [Fact]
    public async Task CheckedRoute_WithoutOriginOrReferer_IsForbidden()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/__test/origin", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status403Forbidden,
            "DOMAIN.FORBIDDEN",
            "error.forbidden");
    }

    [Fact]
    public async Task CheckedRoute_WithConfiguredOrigin_IsAllowed()
    {
        var client = factory.CreateAnonymousClient();

        var response = await SendAsync(client, "/api/v1/__test/origin", origin: ApiTestFactory.TestOrigin, referer: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CheckedRoute_WithForeignOrigin_IsForbidden()
    {
        var client = factory.CreateAnonymousClient();

        var response = await SendAsync(client, "/api/v1/__test/origin", origin: ForeignOrigin, referer: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CheckedRoute_WithConfiguredRefererOnly_IsAllowed()
    {
        var client = factory.CreateAnonymousClient();

        var response = await SendAsync(
            client,
            "/api/v1/__test/origin",
            origin: null,
            referer: $"{ApiTestFactory.TestOrigin}/upload");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CheckedRoute_WithForeignRefererOnly_IsForbidden()
    {
        var client = factory.CreateAnonymousClient();

        var response = await SendAsync(
            client,
            "/api/v1/__test/origin",
            origin: null,
            referer: $"{ForeignOrigin}/upload");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UncheckedRoute_IgnoresForeignOrigin()
    {
        var client = factory.CreateAnonymousClient();

        var response = await SendAsync(client, "/api/v1/__test/anonymous", origin: ForeignOrigin, referer: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
