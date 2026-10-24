using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed class AuthRateLimitTests(TightLimitApiTestFactory factory) : IClassFixture<TightLimitApiTestFactory>
{
    [Fact]
    public async Task Register_returns_429_after_the_permit_limit()
    {
        var client = factory.CreateAnonymousClient();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await client.PostAsJsonAsync(new Uri("/api/v1/auth/register", UriKind.Relative), new
            {
                email = $"bulk{attempt}@example.test",
                name = "Bulk",
                password = "supersecret12",
                tenantName = "Mi tienda",
            });

            if (last.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(last);
        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, last!.StatusCode);

        var problem = await ProblemReader.ReadAsync(last);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.GetProperty("status").GetInt32());
    }
}
