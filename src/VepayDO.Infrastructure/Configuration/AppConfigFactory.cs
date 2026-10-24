using Microsoft.Extensions.Configuration;
using VepayDO.Application.Configuration;

namespace VepayDO.Infrastructure.Configuration;

public static class AppConfigFactory
{
    public static AppConfig Create(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environmentName);

        var section = configuration.GetSection("App");

        var config = new AppConfig
        {
            Environment = environmentName,
            DatabaseUrl = PickRequired(section["DatabaseUrl"], configuration.GetConnectionString("DefaultConnection"), configuration["DATABASE_URL"]),
            DirectUrl = Pick(section["DirectUrl"], configuration["DIRECT_URL"]),
            MaintenanceDatabaseUrl = Pick(section["MaintenanceDatabaseUrl"], configuration["MAINTENANCE_DATABASE_URL"]),
            RedisUrl = Pick(section["RedisUrl"], configuration["REDIS_URL"]),
            JwtAccessSecret = PickRequired(section["JwtAccessSecret"], configuration["JWT_ACCESS_SECRET"]),
            RefreshTokenPepper = PickRequired(section["RefreshTokenPepper"], configuration["REFRESH_TOKEN_PEPPER"]),
            AppOrigin = PickRequired(section["AppOrigin"], configuration["APP_ORIGIN"]),
            BrandName = Pick(section["BrandName"], configuration["BRAND_NAME"]) ?? AppConfig.DefaultBrandName,
            S3Endpoint = Pick(section["S3Endpoint"], configuration["S3_ENDPOINT"]),
            S3Region = Pick(section["S3Region"], configuration["S3_REGION"]),
            S3Bucket = Pick(section["S3Bucket"], configuration["S3_BUCKET"]),
            S3AccessKey = Pick(section["S3AccessKey"], configuration["S3_ACCESS_KEY"]),
            S3SecretKey = Pick(section["S3SecretKey"], configuration["S3_SECRET_KEY"]),
            S3ForcePathStyle = ParseBool(Pick(section["S3ForcePathStyle"], configuration["S3_FORCE_PATH_STYLE"])),
            StorageProvider = Pick(section["StorageProvider"], configuration["STORAGE_PROVIDER"]) ?? StorageProviders.InMemory,
            StoragePublicBaseUrl = Pick(section["StoragePublicBaseUrl"], configuration["STORAGE_PUBLIC_BASE_URL"]) ?? string.Empty,
            StorageSigningSecret = Pick(section["StorageSigningSecret"], configuration["STORAGE_SIGNING_SECRET"]) ?? string.Empty,
            StorageMaxUploadBytes = ParseInt(Pick(section["StorageMaxUploadBytes"], configuration["STORAGE_MAX_UPLOAD_BYTES"])) ?? 10_000_000,
            StorageSignedUrlTtlSeconds = ParseInt(Pick(section["StorageSignedUrlTtlSeconds"], configuration["STORAGE_SIGNED_URL_TTL_SECONDS"])) ?? 300,
            WorkerBatchSize = ParseInt(Pick(section["WorkerBatchSize"], configuration["WORKER_BATCH_SIZE"])) ?? 10,
            WorkerPollIntervalSeconds = ParseInt(Pick(section["WorkerPollIntervalSeconds"], configuration["WORKER_POLL_INTERVAL_SECONDS"])) ?? 5,
            WorkerMaxAttempts = ParseInt(Pick(section["WorkerMaxAttempts"], configuration["WORKER_MAX_ATTEMPTS"])) ?? 5,
            OcrServiceUrl = Pick(section["OcrServiceUrl"], configuration["OCR_SERVICE_URL"]),
            FcmProjectId = Pick(section["FcmProjectId"], configuration["FCM_PROJECT_ID"]),
            FcmClientEmail = Pick(section["FcmClientEmail"], configuration["FCM_CLIENT_EMAIL"]),
            FcmPrivateKey = Pick(section["FcmPrivateKey"], configuration["FCM_PRIVATE_KEY"]),
            SentryDsn = Pick(section["SentryDsn"], configuration["SENTRY_DSN"]),
            MailTransport = Pick(section["MailTransport"], configuration["MAIL_TRANSPORT"]) ?? "smtp",
            MailHost = Pick(section["MailHost"], configuration["MAIL_HOST"]),
            MailPort = ParseInt(Pick(section["MailPort"], configuration["MAIL_PORT"])) ?? 1025,
            MailUser = Pick(section["MailUser"], configuration["MAIL_USER"]),
            MailPassword = Pick(section["MailPassword"], configuration["MAIL_PASSWORD"]),
            MailFrom = Pick(section["MailFrom"], configuration["MAIL_FROM"]) ?? $"VepayDO <no-reply@vepaydo.local>",
            LegalTermsVersion = Pick(section["LegalTermsVersion"], configuration["LEGAL_TERMS_VERSION"]) ?? "2026-01",
            LegalPrivacyVersion = Pick(section["LegalPrivacyVersion"], configuration["LEGAL_PRIVACY_VERSION"]) ?? "2026-01",
        };

        var errors = AppConfigValidator.Validate(config);
        if (errors.Count > 0)
        {
            var details = string.Join(
                Environment.NewLine,
                errors.Select(error => $"  - {error.Path}: {error.Message}"));
            throw new InvalidOperationException(
                $"Invalid application configuration:{Environment.NewLine}{details}");
        }

        var licenseKey = Pick(section["LicenseKey"], configuration["VEPAYDO_LICENSE_KEY"]);
        VepayDO.Infrastructure.Security.LicenseSentinel.AssertValid(environmentName, licenseKey);

        return config;
    }

    private static string? Pick(params string?[] candidates) =>
        candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string PickRequired(params string?[] candidates) =>
        Pick(candidates) ?? string.Empty;

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private static int? ParseInt(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
