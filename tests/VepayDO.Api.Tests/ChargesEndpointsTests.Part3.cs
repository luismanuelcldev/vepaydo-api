using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Tests;

public sealed partial class ChargesEndpointsTests
{

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
}
