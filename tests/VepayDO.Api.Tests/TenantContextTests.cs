using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

/// <summary>
/// The request unit of work: an authenticated call runs inside a tenant context
/// derived from the token, a call without a selected tenant runs as the user,
/// and an anonymous route starts no transaction at all (AGENTS.md section 7).
/// </summary>
public sealed class TenantContextTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task SelectedTenant_ProducesOwnerContext()
    {
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri("/api/v1/__test/context", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(TestData.TenantId, body.GetProperty("tenantId").GetGuid());
        Assert.Equal(TestData.OwnerUserId, body.GetProperty("userId").GetGuid());
        Assert.Equal("Owner", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task NoSelectedTenant_RunsAsTheUser()
    {
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId));

        var response = await client.GetAsync(new Uri("/api/v1/__test/context", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("tenantId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("role").ValueKind);
        Assert.Equal(TestData.OwnerUserId, body.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task AnonymousRoute_StartsNoTenantTransaction()
    {
        var runsBefore = factory.Runner.RunContexts.Count;
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/__test/anonymous", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(runsBefore, factory.Runner.RunContexts.Count);
    }
}
