using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Customers;

namespace VepayDO.Api.Tests;

/// <summary>
/// HTTP contract of the customers module (UC-DATA-01, UC-DATA-02): both roles can
/// read and mutate, but only an OWNER can move an alias between customers.
/// </summary>
public sealed class CustomersEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Verifier_Can_List_Customers()
    {
        factory.Customers.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/customers", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_Can_Create_A_Customer()
    {
        factory.Customers.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/customers", UriKind.Relative),
            JsonBody("""{"name":"María Pérez","phone":null,"notes":null}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("María Pérez", body!.Name);
    }

    [Fact]
    public async Task Create_With_An_Empty_Name_Is_Rejected()
    {
        factory.Customers.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/customers", UriKind.Relative),
            JsonBody("""{"name":"","phone":null,"notes":null}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_Can_Add_An_Alias()
    {
        factory.Customers.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PostAsync(
            new Uri($"/api/v1/customers/{Guid.NewGuid()}/aliases", UriKind.Relative),
            JsonBody("""{"alias":"José Perez"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

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
