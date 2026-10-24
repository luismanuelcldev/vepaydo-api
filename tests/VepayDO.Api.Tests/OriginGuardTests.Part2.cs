using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed partial class OriginGuardTests
{

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        string? origin,
        string? referer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (referer is not null)
        {
            request.Headers.Add("Referer", referer);
        }

        return client.SendAsync(request);
    }
}
