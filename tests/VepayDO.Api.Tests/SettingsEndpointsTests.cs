using System.Net;
using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed partial class SettingsEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory factory = factory;

    [Fact]
    public async Task Verifier_Can_Read_Settings()
    {
        factory.Settings.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.GetAsync(new Uri("/api/v1/settings", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Verifier_Cannot_Update_Settings()
    {
        factory.Settings.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER"));

        var response = await client.PatchAsync(
            new Uri("/api/v1/settings", UriKind.Relative),
            JsonBody("""{"dateWindowDays":30}"""));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status403Forbidden,
            "DOMAIN.FORBIDDEN",
            "error.forbidden");

        Assert.Null(factory.Settings.LastUpdate);
    }

    [Fact]
    public async Task Owner_Can_Update_Settings()
    {
        factory.Settings.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PatchAsync(
            new Uri("/api/v1/settings", UriKind.Relative),
            JsonBody("""{"dateWindowDays":45,"clearVerifierAmountCap":true}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(factory.Settings.LastUpdate);
        Assert.Equal((short)45, factory.Settings.LastUpdate!.DateWindowDays);
        Assert.True(factory.Settings.LastUpdate.ClearVerifierAmountCap);
    }

    [Theory]
    [InlineData("""{"dateWindowDays":0}""")]
    [InlineData("""{"dateWindowDays":61}""")]
    [InlineData("""{"amountTolerance":-1}""")]
    [InlineData("""{"verifierAmountCap":0}""")]
    [InlineData("""{"imageRetentionMonths":0}""")]
    [InlineData("""{"imageRetentionMonths":121}""")]
    public async Task Owner_Update_With_Invalid_Field_Is_Rejected(string body)
    {
        factory.Settings.Reset();
        var client = factory.CreateClientWithToken(
            factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));

        var response = await client.PatchAsync(new Uri("/api/v1/settings", UriKind.Relative), JsonBody(body));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Null(factory.Settings.LastUpdate);
    }
}
