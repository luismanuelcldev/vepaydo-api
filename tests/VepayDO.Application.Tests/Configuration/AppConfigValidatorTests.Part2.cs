using FluentAssertions;
using VepayDO.Application.Configuration;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Configuration;

public sealed partial class AppConfigValidatorTests
{

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
}
