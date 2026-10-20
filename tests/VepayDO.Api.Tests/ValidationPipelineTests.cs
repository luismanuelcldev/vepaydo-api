using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

/// <summary>
/// Request validation is part of the kernel: a body that fails its validator, or
/// that cannot be parsed at all, becomes one 422 problem-details document with
/// the offending fields listed (AGENTS.md section 3.3).
/// </summary>
public sealed class ValidationPipelineTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task ValidBody_ReachesTheAction()
    {
        var client = factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, """{"name":"Ana"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ana", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task InvalidBody_Returns422_WithTheFieldErrors()
    {
        var client = factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, """{"name":""}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status422UnprocessableEntity,
            "DOMAIN.VALIDATION",
            "error.validation");

        var errors = problem.GetProperty("errors");
        Assert.Equal(1, errors.GetArrayLength());
        Assert.Equal("Name", errors[0].GetProperty("path").GetString());
        Assert.Equal("name is required", errors[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task MalformedBody_Returns422()
    {
        var client = factory.CreateAnonymousClient();

        var response = await PostJsonAsync(client, """{"name":""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await ProblemReader.ReadAsync(response);
        ProblemReader.AssertShape(
            problem,
            StatusCodes.Status422UnprocessableEntity,
            "DOMAIN.VALIDATION",
            "error.validation");

        Assert.True(problem.GetProperty("errors").GetArrayLength() > 0);
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return client.PostAsync(new Uri("/api/v1/__test/validation", UriKind.Relative), content);
    }
}
