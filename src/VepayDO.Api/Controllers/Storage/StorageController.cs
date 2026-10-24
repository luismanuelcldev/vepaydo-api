using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using VepayDO.Application.Storage;
using VepayDO.Domain.Time;

namespace VepayDO.Api.Controllers.Storage;

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
