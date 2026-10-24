using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Members;

namespace VepayDO.Api.Tests;

public sealed partial class MembersEndpointsTests
{

    [Fact]
    public async Task Owner_Create_With_An_Invalid_Email_Is_Rejected()
    {
        factory.Members.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/members", UriKind.Relative),
            JsonBody("""{"email":"not-an-email","name":"Nuevo","role":"VERIFIER"}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Null(factory.Members.LastCreate);
    }

    [Fact]
    public async Task Owner_Can_Revoke_A_Members_Sessions()
    {
        factory.Members.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri($"/api/v1/members/{Guid.NewGuid()}/revoke-sessions", UriKind.Relative),
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Cannot_List_Members()
    {
        factory.Members.Reset();
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/members", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonBody(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
