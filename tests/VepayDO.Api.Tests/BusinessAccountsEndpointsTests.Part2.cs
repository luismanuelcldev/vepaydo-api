using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.BusinessAccounts;

namespace VepayDO.Api.Tests;

public sealed partial class BusinessAccountsEndpointsTests
{

    [Fact]
    public async Task Owner_Create_With_An_Unsupported_Currency_Is_Rejected()
    {
        factory.BusinessAccounts.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/business-accounts", UriKind.Relative),
            JsonBody("""{"bankId":1,"label":"Principal","last4":"1234","currency":"EUR"}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Null(factory.BusinessAccounts.LastCreate);
    }

    [Fact]
    public async Task Anonymous_Cannot_List_Accounts()
    {
        factory.BusinessAccounts.Reset();
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/business-accounts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonBody(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
