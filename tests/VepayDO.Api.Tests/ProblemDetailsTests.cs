using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed class ProblemDetailsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        var client = AuthenticatedClient();

        var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(problem, StatusCodes.Status404NotFound, "HTTP.NOT_FOUND", "error.not_found");
    }

    [Fact]
    public async Task UnknownRoute_ForAnAnonymousCaller_Returns401()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongMethod_Returns405ProblemDetails()
    {
        var client = AuthenticatedClient();

        var response = await client.PostAsync(new Uri("/health/live", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status405MethodNotAllowed,
            "HTTP.METHOD_NOT_ALLOWED",
            "error.method_not_allowed");
    }

    [Fact]
    public async Task UnhandledException_Returns500_WithoutLeakingTheMessage()
    {
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/__test/boom", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status500InternalServerError,
            "HTTP.INTERNAL_SERVER_ERROR",
            "error.internal_server_error");

        Assert.Equal("Internal server error", problem.GetProperty("detail").GetString());
        Assert.DoesNotContain("secret internal detail", problem.GetRawText(), StringComparison.Ordinal);
    }

    private HttpClient AuthenticatedClient() =>
        factory.CreateClientWithToken(factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER"));
}
