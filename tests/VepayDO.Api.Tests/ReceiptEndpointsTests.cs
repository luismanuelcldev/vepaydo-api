using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using VepayDO.Api.Tests.Fixtures;
using VepayDO.Contracts.Receipts;

namespace VepayDO.Api.Tests;

public sealed class ReceiptEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory = factory;

    [Fact]
    public async Task List_receipts_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/receipts");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verifier_can_list_receipts()
    {
        var token = _factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER", Guid.NewGuid());
        var client = _factory.CreateClientWithToken(token);

        var response = await client.GetAsync("/api/v1/receipts");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ReceiptPageResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_receipt_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/receipts/{Guid.NewGuid():D}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_image_url_requires_authentication()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/receipts/{Guid.NewGuid():D}/image");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_without_idempotency_key_is_rejected()
    {
        var token = _factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER", Guid.NewGuid());
        var client = _factory.CreateClientWithToken(token);

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "files", "receipt.jpg");

        var response = await client.PostAsync("/api/v1/receipts", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_with_invalid_idempotency_key_is_rejected()
    {
        var token = _factory.CreateAccessToken(TestData.VerifierUserId, TestData.TenantId, "VERIFIER", Guid.NewGuid());
        var client = _factory.CreateClientWithToken(token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", "not-a-uuid");

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "files", "receipt.jpg");

        var response = await client.PostAsync("/api/v1/receipts", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Owner_can_upload_a_receipt()
    {
        var token = _factory.CreateAccessToken(TestData.OwnerUserId, TestData.TenantId, "OWNER", Guid.NewGuid());
        var client = _factory.CreateClientWithToken(token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "files", "receipt.jpg");

        var response = await client.PostAsync("/api/v1/receipts", content);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<ReceiptUploadResponse>();
        body.Should().NotBeNull();
        body!.Receipts.Should().HaveCount(1);
    }
}
