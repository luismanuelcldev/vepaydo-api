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
}
