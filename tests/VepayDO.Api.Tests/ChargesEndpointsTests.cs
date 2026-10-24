using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Tests;

public sealed partial class ChargesEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Verifier_Can_List_Charges()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/charges", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Owner_Can_Create_A_Charge()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/charges", UriKind.Relative),
            JsonBody("""{"customerId":null,"code":"FACT-001","description":"Servicio","amount":1500.00,"currency":"DOP","issuedOn":"2026-03-01","dueOn":null}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChargeResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("FACT-001", body!.Code);
        Assert.Equal(1500.00m, body.Amount);
    }

    [Fact]
    public async Task Create_With_A_Non_Positive_Amount_Is_Rejected()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/charges", UriKind.Relative),
            JsonBody("""{"customerId":null,"code":null,"description":"Servicio","amount":0,"currency":"DOP","issuedOn":"2026-03-01","dueOn":null}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_An_Unsupported_Currency_Is_Rejected()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/charges", UriKind.Relative),
            JsonBody("""{"customerId":null,"code":null,"description":"Servicio","amount":10,"currency":"EUR","issuedOn":"2026-03-01","dueOn":null}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
