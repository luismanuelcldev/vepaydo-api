namespace VepayDO.Application.Configuration;

/// <summary>
/// Strongly-typed, validated application configuration (mirrors the NestJS
/// <c>AppConfig</c>). Bound from environment / configuration at boot; an invalid
/// configuration must fail fast (AGENTS.md section 3.3, section 15).
/// </summary>
public sealed record AppConfig
{
    public const string DefaultBrandName = "VepayDO";
    public const string DefaultTenantTimeZone = "America/Santo_Domingo";

    public string Environment { get; init; } = "Development";

    public string DatabaseUrl { get; init; } = string.Empty;

    public string? DirectUrl { get; init; }

    public string? MaintenanceDatabaseUrl { get; init; }

    public string? RedisUrl { get; init; }

    public string JwtAccessSecret { get; init; } = string.Empty;

    public string RefreshTokenPepper { get; init; } = string.Empty;

    public string? S3Endpoint { get; init; }

    public string? S3Region { get; init; }

    public string? S3Bucket { get; init; }

    public string? S3AccessKey { get; init; }

    public string? S3SecretKey { get; init; }

    public bool S3ForcePathStyle { get; init; }

    /// <summary>
    /// The storage family: <c>inmemory</c> (default, for a local run and the
    /// tests) or <c>s3</c> (any S3-compatible server). The client never sees
    /// this choice: it always asks the API for a signed URL (M5.4).
    /// </summary>
    public string StorageProvider { get; init; } = StorageProviders.InMemory;

    /// <summary>Public base URL used to build the signed URLs of the in-memory family.</summary>
    public string StoragePublicBaseUrl { get; init; } = string.Empty;

    /// <summary>Secret that signs the URLs of the in-memory family.</summary>
    public string StorageSigningSecret { get; init; } = string.Empty;

    /// <summary>Maximum size of an uploaded receipt, in bytes.</summary>
    public int StorageMaxUploadBytes { get; init; } = 10_000_000;

    /// <summary>Lifetime of a signed URL, in seconds.</summary>
    public int StorageSignedUrlTtlSeconds { get; init; } = 300;

    /// <summary>How many receipt jobs the worker claims per cycle (M5.4).</summary>
    public int WorkerBatchSize { get; init; } = 10;

    /// <summary>How long the worker waits between cycles when there is nothing to do, in seconds.</summary>
    public int WorkerPollIntervalSeconds { get; init; } = 5;

    /// <summary>How many times a job is retried before it is parked as failed.</summary>
    public int WorkerMaxAttempts { get; init; } = 5;

    public string? OcrServiceUrl { get; init; }

    public string? FcmProjectId { get; init; }

    public string? FcmClientEmail { get; init; }

    public string? FcmPrivateKey { get; init; }

    public string? SentryDsn { get; init; }

    public string AppOrigin { get; init; } = string.Empty;

    public string BrandName { get; init; } = DefaultBrandName;

    public string MailTransport { get; init; } = "smtp";

    public string? MailHost { get; init; }

    public int MailPort { get; init; } = 1025;

    public string? MailUser { get; init; }

    public string? MailPassword { get; init; }

    public string MailFrom { get; init; } = $"{DefaultBrandName} <no-reply@vepaydo.local>";

    public string LegalTermsVersion { get; init; } = "2026-01";

    public string LegalPrivacyVersion { get; init; } = "2026-01";
}

/// <summary>The supported storage families (M5.4).</summary>
public static class StorageProviders
{
    public const string InMemory = "inmemory";
    public const string S3 = "s3";

    public static bool IsKnown(string? provider) =>
        provider is not null
        && (string.Equals(provider, InMemory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, S3, StringComparison.OrdinalIgnoreCase));
}
