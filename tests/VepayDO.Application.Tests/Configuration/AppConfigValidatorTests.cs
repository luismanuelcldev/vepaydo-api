using FluentAssertions;
using VepayDO.Application.Configuration;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Configuration;

public sealed class AppConfigValidatorTests
{
    private static AppConfig ValidConfig() => new()
    {
        DatabaseUrl = "postgresql://user:pass@localhost:5432/vepaydo",
        JwtAccessSecret = new string('a', 32),
        RefreshTokenPepper = new string('b', 32),
        AppOrigin = "https://app.vepaydo.example",
        MailHost = "localhost",
        StorageProvider = StorageProviders.S3,
        S3Bucket = "vepaydo",
        S3AccessKey = "minioadmin",
        S3SecretKey = "minioadmin",
    };

    [Fact]
    public void Requires_the_in_memory_storage_settings_when_the_provider_is_in_memory()
    {
        AppConfig config = ValidConfig() with
        {
            StorageProvider = StorageProviders.InMemory,
            S3Bucket = null,
            S3AccessKey = null,
            S3SecretKey = null,
        };

        IReadOnlyList<FieldError> errors = AppConfigValidator.Validate(config);

        errors.Should().Contain(e => e.Path == nameof(AppConfig.StoragePublicBaseUrl));
        errors.Should().Contain(e => e.Path == nameof(AppConfig.StorageSigningSecret));
    }

    [Fact]
    public void Rejects_an_unknown_storage_provider()
    {
        AppConfig config = ValidConfig() with { StorageProvider = "ftp" };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.StorageProvider));
    }

    [Fact]
    public void Accepts_a_valid_configuration()
    {
        AppConfigValidator.Validate(ValidConfig()).Should().BeEmpty();
    }

    [Fact]
    public void Requires_database_url()
    {
        AppConfig config = ValidConfig() with { DatabaseUrl = string.Empty };

        AppConfigValidator.Validate(config)
            .Should().ContainSingle(e => e.Path == nameof(AppConfig.DatabaseUrl));
    }

    [Fact]
    public void Rejects_non_absolute_database_url()
    {
        AppConfig config = ValidConfig() with { DatabaseUrl = "not-a-url" };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.DatabaseUrl));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(0)]
    public void Requires_jwt_access_secret_of_at_least_32_characters(int length)
    {
        AppConfig config = ValidConfig() with { JwtAccessSecret = new string('a', length) };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.JwtAccessSecret));
    }

    [Fact]
    public void Requires_refresh_token_pepper()
    {
        AppConfig config = ValidConfig() with { RefreshTokenPepper = "short" };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.RefreshTokenPepper));
    }

    [Fact]
    public void Requires_app_origin()
    {
        AppConfig config = ValidConfig() with { AppOrigin = string.Empty };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.AppOrigin));
    }

    [Fact]
    public void Requires_mail_host_when_transport_is_smtp()
    {
        AppConfig config = ValidConfig() with { MailTransport = "smtp", MailHost = null };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.MailHost));
    }

    [Fact]
    public void Allows_missing_mail_host_for_fake_transport()
    {
        AppConfig config = ValidConfig() with { MailTransport = "fake", MailHost = null };

        AppConfigValidator.Validate(config).Should().BeEmpty();
    }

    [Fact]
    public void Rejects_unknown_mail_transport()
    {
        AppConfig config = ValidConfig() with { MailTransport = "carrier-pigeon" };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.MailTransport));
    }

    [Fact]
    public void Rejects_invalid_optional_url()
    {
        AppConfig config = ValidConfig() with { RedisUrl = "nonsense" };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.RedisUrl));
    }

    [Fact]
    public void Rejects_non_positive_mail_port()
    {
        AppConfig config = ValidConfig() with { MailPort = 0 };

        AppConfigValidator.Validate(config)
            .Should().Contain(e => e.Path == nameof(AppConfig.MailPort));
    }

    [Fact]
    public void Reports_every_problem_at_once()
    {
        var config = new AppConfig();

        IReadOnlyList<FieldError> errors = AppConfigValidator.Validate(config);

        errors.Should().Contain(e => e.Path == nameof(AppConfig.DatabaseUrl));
        errors.Should().Contain(e => e.Path == nameof(AppConfig.JwtAccessSecret));
        errors.Should().Contain(e => e.Path == nameof(AppConfig.RefreshTokenPepper));
        errors.Should().Contain(e => e.Path == nameof(AppConfig.AppOrigin));
    }

    [Fact]
    public void Default_brand_name_is_vepaydo()
    {
        new AppConfig().BrandName.Should().Be("VepayDO");
    }
}
