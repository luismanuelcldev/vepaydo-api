using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Charges;

namespace VepayDO.Api.Tests;

public sealed partial class ChargesEndpointsTests
{

    [Fact]
    public async Task Anonymous_Cannot_List_Charges()
    {
        factory.Charges.Reset();
        var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync(new Uri("/api/v1/charges", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static StringContent JsonBody(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static MultipartFormDataContent CsvBody(string csv)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "charges.csv");
        return content;
    }
}
