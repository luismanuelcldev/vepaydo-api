using System.Net;
using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed partial class SettingsEndpointsTests
{

    [Fact]
    public async Task Anonymous_Cannot_Read_Settings()
    {
        factory.Settings.Reset();
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/settings", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonBody(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");
}
