using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

public sealed partial class AuthEndpointsTests
{

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
}
