using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Customers;

namespace VepayDO.Api.Tests;

public sealed partial class CustomersEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory factory = factory;

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
}
