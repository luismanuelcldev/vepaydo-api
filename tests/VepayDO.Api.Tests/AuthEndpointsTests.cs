using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

public sealed partial class AuthEndpointsTests : IClassFixture<ApiTestFactory>
{
    private const string AuthBase = "/api/v1/auth";

    private readonly ApiTestFactory _factory;

    public AuthEndpointsTests(ApiTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;

        _factory.Auth.Reset();
    }

    [Fact]
    public async Task Register_returns_201_and_sets_the_refresh_cookie()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/register", new
        {
            email = "new@example.test",
            name = "New Owner",
            password = "supersecret12",
            tenantName = "Mi tienda",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertRefreshCookie(response, present: true);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Login_returns_200_and_sets_the_refresh_cookie()
    {
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "supersecret12",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRefreshCookie(response, present: true);
    }

    [Fact]
    public async Task Login_maps_bad_credentials_to_401_problem_details()
    {
        _factory.Auth.LoginError = new UnauthorizedException("Invalid credentials");
        var client = _factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, $"{AuthBase}/login", new
        {
            email = "owner@example.test",
            password = "wrongpassword1",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status401Unauthorized,
            "DOMAIN.UNAUTHORIZED",
            "error.unauthorized");
    }
}
