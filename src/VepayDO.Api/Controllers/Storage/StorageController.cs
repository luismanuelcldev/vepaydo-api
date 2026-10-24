using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using VepayDO.Application.Storage;
using VepayDO.Domain.Time;

namespace VepayDO.Api.Controllers.Storage;

/// <summary>
/// Serves the objects of the in-memory storage family (M5.4). The client never
/// talks to the storage directly: it asks <c>GET /receipts/{id}/image</c> for a
/// signed URL and fetches it from here, where the signature and the expiry are
/// the only credentials needed.
///
/// <para>
/// With the S3 family the client receives a presigned URL of the object store
/// and never reaches this endpoint.
/// </para>
/// </summary>
[ApiController]
[Route("storage")]
[AllowAnonymous]
public sealed class StorageController(IStorageToolkit storage, IClock clock) : ControllerBase
{
    [HttpGet("{**key}")]
    public async Task<IActionResult> Get(
        string key,
        [FromQuery] long expires,
        [FromQuery] string? signature,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return NotFound();
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expires);

        if (!storage.SignedUrls.IsSignatureValid(key, expiresAt, signature, clock.UtcNow))
        {
            // An invalid or expired signature is a 404: the object exists for
            // nobody without a server-issued URL.
            return NotFound();
        }

        var stored = await storage.Objects.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return NotFound();
        }

        using (stored)
        {
            Response.Headers.CacheControl = "private, no-store";
            return File(stored.Content, stored.ContentType);
        }
    }
}
