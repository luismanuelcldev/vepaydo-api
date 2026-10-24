using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

public sealed partial class AuthEndpointsTests
{

    [Fact]
    public async Task Select_tenant_with_a_token_returns_a_new_access_token()
    {
        _factory.Auth.SelectTenantError = null;
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await PostJsonAsync(
            client,
            $"{AuthBase}/select-tenant",
            new { tenantId = TestData.TenantId },
            origin: ApiTestFactory.TestOrigin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestData.TenantId, _factory.Auth.LastSelectedTenantId);
    }

    [Fact]
    public async Task Me_returns_the_profile_for_an_authenticated_caller()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri($"{AuthBase}/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("owner@example.test", payload.RootElement.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri($"{AuthBase}/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_returns_204_and_clears_the_refresh_cookie()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(new Uri($"{AuthBase}/logout", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, _factory.Auth.LogoutCalls);
        AssertRefreshCookie(response, present: false);
    }

    [Fact]
    public async Task Change_password_returns_a_fresh_access_token()
    {
        _factory.Auth.ChangePasswordError = null;
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await PostJsonAsync(client, $"{AuthBase}/change-password", new
        {
            currentPassword = "supersecret12",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("supersecret12", _factory.Auth.LastChangePasswordCurrent);
    }
}
