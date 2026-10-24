using VepayDO.Domain.Errors;
using static VepayDO.Application.Configuration.ConfigValidationHelpers;

namespace VepayDO.Application.Configuration;

public static class AppConfigValidator
{
    private const int MinSecretLength = 32;

    public static IReadOnlyList<FieldError> Validate(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var errors = new List<FieldError>();

        RequiredUrl(config.DatabaseUrl, nameof(AppConfig.DatabaseUrl), errors);
        OptionalUrl(config.DirectUrl, nameof(AppConfig.DirectUrl), errors);
        OptionalUrl(config.MaintenanceDatabaseUrl, nameof(AppConfig.MaintenanceDatabaseUrl), errors);
        OptionalUrl(config.RedisUrl, nameof(AppConfig.RedisUrl), errors);
        OptionalUrl(config.S3Endpoint, nameof(AppConfig.S3Endpoint), errors);
        OptionalUrl(config.OcrServiceUrl, nameof(AppConfig.OcrServiceUrl), errors);
        OptionalUrl(config.SentryDsn, nameof(AppConfig.SentryDsn), errors);
        RequiredUrl(config.AppOrigin, nameof(AppConfig.AppOrigin), errors);

        MinLength(config.JwtAccessSecret, MinSecretLength, nameof(AppConfig.JwtAccessSecret), errors);
        MinLength(config.RefreshTokenPepper, MinSecretLength, nameof(AppConfig.RefreshTokenPepper), errors);
        MinLength(config.BrandName, 1, nameof(AppConfig.BrandName), errors);
        MinLength(config.MailFrom, 1, nameof(AppConfig.MailFrom), errors);
        MinLength(config.LegalTermsVersion, 1, nameof(AppConfig.LegalTermsVersion), errors);
        MinLength(config.LegalPrivacyVersion, 1, nameof(AppConfig.LegalPrivacyVersion), errors);

        if (config.MailTransport is not ("smtp" or "fake")) errors.Add(new FieldError(nameof(AppConfig.MailTransport), "must be 'smtp' or 'fake'"));
        if (config.MailPort <= 0) errors.Add(new FieldError(nameof(AppConfig.MailPort), "must be a positive integer"));
        if (config.MailTransport == "smtp" && string.IsNullOrWhiteSpace(config.MailHost)) errors.Add(new FieldError(nameof(AppConfig.MailHost), "is required when MailTransport is 'smtp'"));

        if (!StorageProviders.IsKnown(config.StorageProvider))
            errors.Add(new FieldError(nameof(AppConfig.StorageProvider), $"must be '{StorageProviders.InMemory}' or '{StorageProviders.S3}'"));

        if (string.Equals(config.StorageProvider, StorageProviders.InMemory, StringComparison.OrdinalIgnoreCase))
        {
            RequiredUrl(config.StoragePublicBaseUrl, nameof(AppConfig.StoragePublicBaseUrl), errors);
            MinLength(config.StorageSigningSecret, MinSecretLength, nameof(AppConfig.StorageSigningSecret), errors);
        }
        else
        {
            RequiredValue(config.S3Bucket, nameof(AppConfig.S3Bucket), errors);
            RequiredValue(config.S3AccessKey, nameof(AppConfig.S3AccessKey), errors);
            RequiredValue(config.S3SecretKey, nameof(AppConfig.S3SecretKey), errors);
        }

        if (config.StorageMaxUploadBytes <= 0) errors.Add(new FieldError(nameof(AppConfig.StorageMaxUploadBytes), "must be a positive integer"));
        if (config.StorageSignedUrlTtlSeconds < 30) errors.Add(new FieldError(nameof(AppConfig.StorageSignedUrlTtlSeconds), "must be at least 30 seconds"));
        if (config.WorkerBatchSize <= 0) errors.Add(new FieldError(nameof(AppConfig.WorkerBatchSize), "must be a positive integer"));
        if (config.WorkerPollIntervalSeconds <= 0) errors.Add(new FieldError(nameof(AppConfig.WorkerPollIntervalSeconds), "must be a positive integer"));
        if (config.WorkerMaxAttempts <= 0) errors.Add(new FieldError(nameof(AppConfig.WorkerMaxAttempts), "must be a positive integer"));

        return errors;
    }
}
