using Microsoft.AspNetCore.Http;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Controllers.Receipts;

internal static class FormFileExtensions
{
    public static async Task<byte[]> ReadBytesAsync(this IFormFile file, long maxBytes, CancellationToken cancellationToken)
    {
        if (file.Length == 0) throw new InvalidArgumentDomainException("An uploaded file is empty");

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream(capacity: (int)Math.Min(file.Length, 1_048_576));
        var chunk = new byte[81_920];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes)
                throw new PayloadTooLargeException($"Each file must be at most {maxBytes} bytes");

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
