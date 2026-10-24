using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Tests;

/// <summary>
/// HTTP contract of the charges module (UC-DATA-03 to 06): both roles can read and
/// mutate, validation rejects malformed payloads, and the CSV import reports the
/// parsed rows.
/// </summary>
public sealed class ChargesEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
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

    [Fact]
    public async Task Create_With_A_Due_Date_Before_The_Issue_Date_Is_Rejected()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/charges", UriKind.Relative),
            JsonBody("""{"customerId":null,"code":null,"description":"Servicio","amount":10,"currency":"DOP","issuedOn":"2026-03-10","dueOn":"2026-03-01"}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_Can_Void_A_Charge()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PostAsync(
            new Uri($"/api/v1/charges/{Guid.NewGuid()}/void", UriKind.Relative),
            JsonBody("""{"reason":"Creada por error"}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Void_Without_A_Reason_Is_Rejected()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri($"/api/v1/charges/{Guid.NewGuid()}/void", UriKind.Relative),
            JsonBody("""{"reason":""}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_Returns_A_New_Charge()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri($"/api/v1/charges/{Guid.NewGuid()}/duplicate", UriKind.Relative),
            JsonBody("""{"code":null}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Import_Parses_The_Csv_Rows()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var csv = """
            code,description,amount,currency,issued_on,due_on,customer_id
            FACT-001,Servicio de marzo,1500.00,DOP,2026-03-01,2026-03-31,
            """;

        var response = await client.PostAsync(
            new Uri("/api/v1/charges/import?allOrNothing=false", UriKind.Relative),
            CsvBody(csv));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ChargeImportResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.Imported);
        Assert.NotNull(factory.Charges.LastImport);
        Assert.Single(factory.Charges.LastImport!);
        Assert.Equal("FACT-001", factory.Charges.LastImport![0].Code);
        Assert.Equal(1500.00m, factory.Charges.LastImport[0].Amount);
        Assert.Equal(new DateOnly(2026, 3, 31), factory.Charges.LastImport[0].DueOn);
    }

    [Fact]
    public async Task Import_With_An_Invalid_Header_Is_Rejected()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/charges/import", UriKind.Relative),
            CsvBody("nombre,monto\nServicio,10\n"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task List_With_An_Unknown_Status_Is_Rejected()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/charges?status=NOPE", UriKind.Relative));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task List_With_A_Known_Status_Is_Accepted()
    {
        factory.Charges.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/charges?status=OPEN", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_Cannot_List_Charges()
    {
        factory.Charges.Reset();
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/charges", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonBody(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static MultipartFormDataContent CsvBody(string csv)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "charges.csv");
        return content;
    }
}
