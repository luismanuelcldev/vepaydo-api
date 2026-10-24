using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using VepayDO.Api.Kernel.Attributes;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Receipts;
using VepayDO.Application.Security;
using VepayDO.Contracts.Receipts;
using VepayDO.Domain.Enums;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Receipts;

namespace VepayDO.Api.Controllers.Receipts;

/// <summary>
/// Receipt endpoints under <c>/api/v1/receipts</c> (UC-RCP-01 to UC-RCP-03).
/// Upload, read and image access are allowed to OWNER and VERIFIER
/// (<c>receipts.review</c>, AGENTS.md section 9.5).
/// </summary>
[ApiController]
[Route("receipts")]
public sealed class ReceiptsController(IReceiptService receipts, ReceiptOptions options) : ControllerBase
{
    /// <summary>Maximum number of files in one upload (INTEGRATION.md section 5).</summary>
    public const int MaxFilesPerRequest = 20;

    /// <summary>Maximum size of the whole request: 20 files of 10 MB plus the form overhead.</summary>
    private const long MaxRequestBytes = 210_000_000;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequirePermission(Permissions.ReceiptsReview)]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<ReceiptUploadResponse>> Upload(
        [FromForm] List<IFormFile>? files,
        [FromForm] string? source,
        CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0)
        {
            throw new InvalidArgumentDomainException("At least one file is required");
        }

        if (files.Count > MaxFilesPerRequest)
        {
            throw new InvalidArgumentDomainException(
                $"At most {MaxFilesPerRequest} files can be uploaded in one request");
        }

        var idempotencyKey = IdempotencyKey.Parse(Request.Headers["Idempotency-Key"].FirstOrDefault());
        var receiptSource = ParseSource(source);
        var meta = RequestMetaFactory.From(HttpContext);

        var created = new List<ReceiptView>(files.Count);

        foreach (var file in files)
        {
            var content = await ReadAsync(file, cancellationToken).ConfigureAwait(false);

            created.Add(await receipts.UploadAsync(
                new ReceiptUpload(idempotencyKey, receiptSource, file.ContentType, content),
                meta,
                cancellationToken).ConfigureAwait(false));
        }

        var response = new ReceiptUploadResponse(created.ToResponse());

        if (created.Count == 1)
        {
            Response.Headers.Location = $"/api/v1/receipts/{created[0].Id:D}";
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet]
    [RequirePermission(Permissions.ReceiptsReview)]
    public async Task<ActionResult<ReceiptPageResponse>> List(
        [FromQuery] ReceiptListRequest query,
        CancellationToken cancellationToken)
    {
        var page = await receipts.ListAsync(
            new ReceiptQuery(
                query.ProcessingStatus,
                query.ReconciliationStatus,
                query.Risk,
                query.CreatedFrom,
                query.CreatedTo,
                query.Limit ?? 20),
            query.Cursor,
            cancellationToken).ConfigureAwait(false);

        return Ok(new ReceiptPageResponse(page.Items.ToResponse(), page.NextCursor));
    }

    [HttpGet("{receiptId:guid}")]
    [RequirePermission(Permissions.ReceiptsReview)]
    public async Task<ActionResult<ReceiptResponse>> Get(Guid receiptId, CancellationToken cancellationToken)
    {
        var receipt = await receipts.GetAsync(receiptId, cancellationToken).ConfigureAwait(false);
        return Ok(receipt.ToResponse());
    }

    [HttpGet("{receiptId:guid}/image")]
    [RequirePermission(Permissions.ReceiptsReview)]
    public async Task<ActionResult<ReceiptImageUrlResponse>> GetImageUrl(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var url = await receipts
            .GetImageUrlAsync(receiptId, RequestMetaFactory.From(HttpContext), cancellationToken)
            .ConfigureAwait(false);

        return Ok(new ReceiptImageUrlResponse(url.Url, url.ExpiresAt));
    }

    private static ReceiptSource ParseSource(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return ReceiptSource.WEB_UPLOAD;
        }

        if (!Enum.TryParse<ReceiptSource>(trimmed, ignoreCase: true, out var source))
        {
            throw new InvalidArgumentDomainException(
                $"The source must be one of {string.Join(", ", Enum.GetNames<ReceiptSource>())}");
        }

        return source;
    }

    private async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidArgumentDomainException("An uploaded file is empty");
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream(capacity: (int)Math.Min(file.Length, 1_048_576));

        var chunk = new byte[81_920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > options.MaxUploadBytes)
            {
                throw new PayloadTooLargeException(
                    $"Each file must be at most {options.MaxUploadBytes} bytes");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
