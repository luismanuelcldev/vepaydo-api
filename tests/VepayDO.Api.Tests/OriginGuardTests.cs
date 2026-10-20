using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

/// <summary>
/// CSRF protection for the cookie-authenticated endpoints: a request must come
/// from the configured origin, by <c>Origin</c> or by <c>Referer</c>, and only
/// the routes that ask for it are guarded (AGENTS.md section 13).
/// </summary>
public sealed class OriginGuardTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
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

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        string? origin,
        string? referer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (referer is not null)
        {
            request.Headers.Add("Referer", referer);
        }

        return client.SendAsync(request);
    }
}
