using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Members;

namespace VepayDO.Api.Tests;

public sealed partial class MembersEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Verifier_Cannot_List_Members()
    {
        factory.Members.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/members", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_Cannot_Create_A_Member()
    {
        factory.Members.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/members", UriKind.Relative),
            JsonBody("""{"email":"v2@example.test","name":"V2","role":"VERIFIER"}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(factory.Members.LastCreate);
    }

    [Fact]
    public async Task Owner_Can_List_Members()
    {
        factory.Members.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.GetAsync(new Uri("/api/v1/members", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Owner_Create_Returns_The_Temporary_Password_Once()
    {
        factory.Members.Reset();
        factory.Members.TemporaryPassword = "one-time-pass-1234";

        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PostAsync(
            new Uri("/api/v1/members", UriKind.Relative),
            JsonBody("""{"email":"nuevo@example.test","name":"Nuevo","role":"VERIFIER"}"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("one-time-pass-1234", raw);

        var body = await response.Content.ReadFromJsonAsync<CreateMemberResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("one-time-pass-1234", body!.TemporaryPassword);
        Assert.Equal("nuevo@example.test", body.Member.Email);
    }
}
