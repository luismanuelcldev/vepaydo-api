using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed class HealthEndpointTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Live_IsAnonymous_AndReportsAlive()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("alive", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_FailsWith503_WhenDatabaseIsNotReachable()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status503ServiceUnavailable,
            "HEALTH.DATABASE_UNAVAILABLE",
            "health.database_unavailable");
    }
}
