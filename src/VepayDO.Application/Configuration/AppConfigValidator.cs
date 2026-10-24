using VepayDO.Domain.Errors;

namespace VepayDO.Application.Configuration;

/// <summary>
/// Validates <see cref="AppConfig"/> at boot and returns every problem at once so
/// a misconfiguration is fixed in one pass (AGENTS.md section 3.3).
/// </summary>
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

        if (config.MailTransport is not ("smtp" or "fake"))
        {
            errors.Add(new FieldError(nameof(AppConfig.MailTransport), "must be 'smtp' or 'fake'"));
        }

        if (config.MailPort <= 0)
        {
            errors.Add(new FieldError(nameof(AppConfig.MailPort), "must be a positive integer"));
        }

        if (config.MailTransport == "smtp" && string.IsNullOrWhiteSpace(config.MailHost))
        {
            errors.Add(new FieldError(nameof(AppConfig.MailHost), "is required when MailTransport is 'smtp'"));
        }

        if (!StorageProviders.IsKnown(config.StorageProvider))
        {
            errors.Add(new FieldError(
                nameof(AppConfig.StorageProvider),
                $"must be '{StorageProviders.InMemory}' or '{StorageProviders.S3}'"));
        }

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

        if (config.StorageMaxUploadBytes <= 0)
        {
            errors.Add(new FieldError(nameof(AppConfig.StorageMaxUploadBytes), "must be a positive integer"));
        }

        if (config.StorageSignedUrlTtlSeconds < 30)
        {
            errors.Add(new FieldError(nameof(AppConfig.StorageSignedUrlTtlSeconds), "must be at least 30 seconds"));
        }

        if (config.WorkerBatchSize <= 0)
        {
            errors.Add(new FieldError(nameof(AppConfig.WorkerBatchSize), "must be a positive integer"));
        }

        if (config.WorkerPollIntervalSeconds <= 0)
        {
            errors.Add(new FieldError(nameof(AppConfig.WorkerPollIntervalSeconds), "must be a positive integer"));
        }

        if (config.WorkerMaxAttempts <= 0)
        {
            errors.Add(new FieldError(nameof(AppConfig.WorkerMaxAttempts), "must be a positive integer"));
        }

        return errors;
    }

    private static void RequiredValue(string? value, string path, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new FieldError(path, "is required"));
        }
    }

    private static void RequiredUrl(string? value, string path, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new FieldError(path, "is required"));
            return;
        }

        if (!IsAbsoluteUrl(value))
        {
            errors.Add(new FieldError(path, "must be an absolute URL"));
        }
    }

    private static void OptionalUrl(string? value, string path, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!IsAbsoluteUrl(value))
        {
            errors.Add(new FieldError(path, "must be an absolute URL when set"));
        }
    }

    private static void MinLength(string? value, int minLength, string path, List<FieldError> errors)
    {
        if (string.IsNullOrEmpty(value) || value.Length < minLength)
        {
            errors.Add(new FieldError(path, $"must be at least {minLength} characters"));
        }
    }

    private static bool IsAbsoluteUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Scheme);
}
