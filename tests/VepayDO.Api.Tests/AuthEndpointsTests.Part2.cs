using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

public sealed partial class AuthEndpointsTests
{

    [Fact]
    public async Task Register_rejects_a_short_password_with_422()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/register", new
        {
            email = "new@example.test",
            name = "New Owner",
            password = "short",
            tenantName = "Mi tienda",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_an_invalid_email_with_422()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/register", new
        {
            email = "not-an-email",
            name = "New Owner",
            password = "supersecret12",
            tenantName = "Mi tienda",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_origin_is_forbidden_by_the_csrf_guard()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/refresh", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_the_configured_origin_returns_a_new_token()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(
            client,
            $"{AuthBase}/refresh",
            new { },
            origin: ApiTestFactory.TestOrigin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRefreshCookie(response, present: true);
    }

    [Fact]
    public async Task Select_tenant_requires_authentication()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(
            client,
            $"{AuthBase}/select-tenant",
            new { tenantId = TestData.TenantId },
            origin: ApiTestFactory.TestOrigin);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
