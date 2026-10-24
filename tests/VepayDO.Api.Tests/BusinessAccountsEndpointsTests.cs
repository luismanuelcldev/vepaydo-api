using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.BusinessAccounts;

namespace VepayDO.Api.Tests;

public sealed partial class BusinessAccountsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Verifier_Cannot_List_Accounts()
    {
        factory.BusinessAccounts.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/business-accounts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_Cannot_Create_An_Account()
    {
        factory.BusinessAccounts.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/business-accounts", UriKind.Relative),
            JsonBody("""{"bankId":1,"label":"Principal","last4":"1234","currency":"DOP"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.BusinessAccounts.LastCreate);
    }

    [Fact]
    public async Task Owner_Can_Create_An_Account_And_Only_Last4_Is_Returned()
    {
        factory.BusinessAccounts.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/business-accounts", UriKind.Relative),
            JsonBody("""{"bankId":1,"label":"Principal","last4":"4321","currency":"DOP"}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BusinessAccountResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("4321", body!.Last4);
        Assert.Equal("DOP", body.Currency);
    }

    [Fact]
    public async Task Owner_Create_With_A_Malformed_Last4_Is_Rejected()
    {
        factory.BusinessAccounts.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/business-accounts", UriKind.Relative),
            JsonBody("""{"bankId":1,"label":"Principal","last4":"12ab","currency":"DOP"}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Null(factory.BusinessAccounts.LastCreate);
    }
}
