using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Customers;

namespace VepayDO.Api.Tests;

public sealed partial class CustomersEndpointsTests
{

    [Fact]
    public async Task Verifier_Cannot_Move_An_Alias()
    {
        factory.Customers.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PostAsync(
            new Uri($"/api/v1/customers/{Guid.NewGuid()}/aliases/move", UriKind.Relative),
            JsonBody($$"""{"fromCustomerId":"{{Guid.NewGuid()}}","alias":"José"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.Customers.LastMove);
    }

    [Fact]
    public async Task Owner_Can_Move_An_Alias()
    {
        factory.Customers.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var from = Guid.NewGuid();
        var response = await client.PostAsync(
            new Uri($"/api/v1/customers/{Guid.NewGuid()}/aliases/move", UriKind.Relative),
            JsonBody($$"""{"fromCustomerId":"{{from}}","alias":"José"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.Customers.LastMove);
        Assert.Equal(from, factory.Customers.LastMove!.FromCustomerId);
    }

    [Fact]
    public async Task Anonymous_Cannot_List_Customers()
    {
        factory.Customers.Reset();
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/customers", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonBody(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
