using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Tests;

public sealed partial class AuthEndpointsTests
{

    private static void AssertRefreshCookie(HttpResponseMessage response, bool present)
    {
        var value = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("vepaydo_refresh=", StringComparison.Ordinal))
            : null;

        if (!present)
        {

            Assert.True(value is null || value.StartsWith("vepaydo_refresh=;", StringComparison.Ordinal));
            return;
        }

        Assert.NotNull(value);
        Assert.False(
            value!.StartsWith("vepaydo_refresh=;", StringComparison.Ordinal),
            "expected a populated refresh cookie");
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client,
        string path,
        object body,
        string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative))
        {
            Content = ToJsonContent(body),
        };

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return await client.SendAsync(request);
    }

    private static JsonContent ToJsonContent(object body) =>
        JsonContent.Create(body, options: JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
