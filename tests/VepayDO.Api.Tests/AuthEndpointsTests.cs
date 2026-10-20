using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

/// <summary>
/// HTTP contract of the auth endpoints (AGENTS.md section 10): verbs, status
/// codes, refresh cookie handling, CSRF origin guard, request validation and
/// problem-details mapping. The use cases are replaced by
/// <see cref="FakeAuthService"/>, so these tests do not need PostgreSQL.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<ApiTestFactory>
{
    private const string AuthBase = "/api/v1/auth";

    private readonly ApiTestFactory _factory;

    public AuthEndpointsTests(ApiTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;

        // The factory is shared by the whole class; xUnit builds a new test-class
        // instance per test, so resetting the fake here keeps the tests independent.
        _factory.Auth.Reset();
    }

    [Fact]
    public async Task Register_returns_201_and_sets_the_refresh_cookie()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/register", new
        {
            email = "new@example.test",
            name = "New Owner",
            password = "supersecret12",
            tenantName = "Mi tienda",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertRefreshCookie(response, present: true);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Login_returns_200_and_sets_the_refresh_cookie()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "supersecret12",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRefreshCookie(response, present: true);
    }

    [Fact]
    public async Task Login_maps_bad_credentials_to_401_problem_details()
    {
        _factory.Auth.LoginError = new UnauthorizedException("Invalid credentials");
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "wrongpassword1",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status401Unauthorized,
            "DOMAIN.UNAUTHORIZED",
            "error.unauthorized");
    }

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

    [Fact]
    public async Task Change_password_maps_a_wrong_password_to_401()
    {
        _factory.Auth.ChangePasswordError = new UnauthorizedException("The current password is incorrect");
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await PostJsonAsync(client, $"{AuthBase}/change-password", new
        {
            currentPassword = "wrongpassword1",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_is_neutral_and_always_200()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/forgot-password", new
        {
            email = "someone@example.test",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("someone@example.test", _factory.Auth.LastForgotEmail);
    }

    [Fact]
    public async Task Reset_password_returns_204()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/reset-password", new
        {
            token = "a-reset-token",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("a-reset-token", _factory.Auth.LastResetToken);
    }

    [Fact]
    public async Task Reset_password_maps_an_invalid_token_to_401()
    {
        _factory.Auth.ResetPasswordError = new UnauthorizedException("Invalid or expired reset token");
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/reset-password", new
        {
            token = "stale",
            newPassword = "brand-new-secret12",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_locale_returns_the_new_locale()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var request = new HttpRequestMessage(HttpMethod.Patch, new Uri($"{AuthBase}/me", UriKind.Relative))
        {
            Content = ToJsonContent(new { preferredLocale = "en" }),
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("en", _factory.Auth.LastLocale);
    }

    [Fact]
    public async Task List_sessions_returns_the_canned_sessions()
    {
        _factory.Auth.Sessions =
        [
            new VepayDO.Application.Auth.AuthSessionView(
                Guid.NewGuid(),
                "Android",
                null,
                DateTimeOffset.UtcNow.AddDays(-1),
                null,
                Current: true),
        ];

        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri($"{AuthBase}/sessions", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, payload.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Revoke_all_other_sessions_returns_204()
    {
        var client = _factory.CreateClientWithToken(
            _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri($"{AuthBase}/sessions/revoke-all", UriKind.Relative),
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, _factory.Auth.RevokeAllCalls);
    }

    private static void AssertRefreshCookie(HttpResponseMessage response, bool present)
    {
        var value = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("vepaydo_refresh=", StringComparison.Ordinal))
            : null;

        if (!present)
        {
            // Clearing emits "vepaydo_refresh=; expires=..." (an empty value).
            Assert.True(value is null || value.StartsWith("vepaydo_refresh=;", StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(value);
        Assert.False(
            value!.StartsWith("vepaydo_refresh=;", StringComparison.Ordinal),
            "expected a populated refresh cookie");
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string path,
        object body,
        string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = ToJsonContent(body),
        };

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await client.SendAsync(request);
    }

    private static JsonContent ToJsonContent(object body) =>
        JsonContent.Create(body, options: JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
