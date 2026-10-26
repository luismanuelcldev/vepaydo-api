using System.Text.Json;
using VepayDO.Api.Controllers.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

/// <summary>
/// Native-client (X-Client-Type: native) behavior for register, login, refresh,
/// logout and select-tenant, per ADR 0006. Browser behavior is covered by the
/// other AuthEndpointsTests parts.
/// </summary>
public sealed partial class AuthEndpointsTests
{

    [Fact]
    public async Task Refresh_for_a_native_client_without_origin_returns_the_rotated_token_in_the_body()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await SendNativeAsync(
            client,
            $"{AuthBase}/refresh",
            new RefreshRequest(RefreshToken: "native-refresh-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("native-refresh-token", _factory.Auth.LastRefreshToken);
        AssertRefreshCookie(response, present: true);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("refreshToken", out var refreshToken));
        Assert.Equal(_factory.Auth.RefreshToken, refreshToken.GetString());
    }

    [Fact]
    public async Task Refresh_for_a_native_client_without_a_usable_token_is_unauthorized()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await SendNativeAsync(client, $"{AuthBase}/refresh", new RefreshRequest(RefreshToken: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_for_a_browser_client_does_not_expose_the_refresh_token_in_the_body()
    {
        var client = _factory.CreateAnonymousClient();

        var login = await PostJsonAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "supersecret12",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var response = await PostJsonAsync(client, $"{AuthBase}/refresh", new { }, origin: ApiTestFactory.TestOrigin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRefreshCookie(response, present: true);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(
            payload.RootElement.TryGetProperty("refreshToken", out _),
            "browser clients must never receive the refresh token in the body");
    }

    [Fact]
    public async Task Select_tenant_for_a_native_client_without_origin_is_allowed()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await SendNativeAsync(
            client,
            $"{AuthBase}/select-tenant",
            new SelectTenantRequest(TestData.TenantId, RefreshToken: "native-refresh-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestData.TenantId, _factory.Auth.LastSelectedTenantId);
    }

    [Fact]
    public async Task Select_tenant_for_a_browser_client_without_origin_is_still_forbidden()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await PostJsonAsync(
            client,
            $"{AuthBase}/select-tenant",
            new SelectTenantRequest(TestData.TenantId, RefreshToken: "native-refresh-token"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_for_a_native_client_revokes_the_body_token_session()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await SendNativeAsync(client, $"{AuthBase}/logout", new LogoutRequest(RefreshToken: "native-refresh-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("native-refresh-token", _factory.Auth.LastLogoutRefreshToken);
    }

    [Fact]
    public async Task Login_for_a_native_client_returns_the_token_in_the_body_without_an_origin()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await SendNativeAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "supersecret12",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRefreshCookie(response, present: true);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("refreshToken", out var refreshToken));
        Assert.Equal(_factory.Auth.RefreshToken, refreshToken.GetString());
    }

    [Fact]
    public async Task Login_for_a_browser_client_does_not_expose_the_refresh_token_in_the_body()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "supersecret12",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(
            payload.RootElement.TryGetProperty("refreshToken", out _),
            "browser clients must never receive the refresh token in the body");
    }

    private static async Task<HttpResponseMessage> SendNativeAsync(
        HttpClient client,
        string path,
        object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = ToJsonContent(body),
        };
        request.Headers.Add(ClientTypeHeaders.Name, ClientTypeHeaders.Native);

        return await client.SendAsync(request);
    }
}
