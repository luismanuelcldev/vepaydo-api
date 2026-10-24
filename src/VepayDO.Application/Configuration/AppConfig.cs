namespace VepayDO.Application.Configuration;

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

    public string StorageProvider { get; init; } = StorageProviders.InMemory;

    public string StoragePublicBaseUrl { get; init; } = string.Empty;

    public string StorageSigningSecret { get; init; } = string.Empty;

    public int StorageMaxUploadBytes { get; init; } = 10_000_000;

    public int StorageSignedUrlTtlSeconds { get; init; } = 300;

    public int WorkerBatchSize { get; init; } = 10;

    public int WorkerPollIntervalSeconds { get; init; } = 5;

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

public static class StorageProviders
{
    public const string InMemory = "inmemory";
    public const string S3 = "s3";

    public static bool IsKnown(string? provider) =>
        provider is not null
        && (string.Equals(provider, InMemory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, S3, StringComparison.OrdinalIgnoreCase));
}
