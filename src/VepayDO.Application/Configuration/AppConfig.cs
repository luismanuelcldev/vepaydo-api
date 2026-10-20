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
