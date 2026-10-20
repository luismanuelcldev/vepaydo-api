using FluentAssertions;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tests;

public sealed class DomainExceptionTests
{
    [Fact]
    public void NotFound_exposes_stable_code_and_status()
    {
        var exception = new NotFoundException();

        exception.Code.Should().Be("DOMAIN.NOT_FOUND");
        exception.I18nKey.Should().Be("error.not_found");
        exception.HttpStatus.Should().Be(404);
    }

    [Fact]
    public void Conflict_maps_to_409()
    {
        new ConflictException().HttpStatus.Should().Be(409);
    }

    [Fact]
    public void Forbidden_maps_to_403()
    {
        new ForbiddenException().HttpStatus.Should().Be(403);
    }

    [Fact]
    public void Unauthorized_maps_to_401()
    {
        new UnauthorizedException().HttpStatus.Should().Be(401);
    }

    [Fact]
    public void Password_change_required_uses_auth_code()
    {
        var exception = new PasswordChangeRequiredException();

        exception.Code.Should().Be("AUTH.PASSWORD_CHANGE_REQUIRED");
        exception.HttpStatus.Should().Be(403);
    }

    [Fact]
    public void Legal_acceptance_required_uses_legal_code()
    {
        var exception = new LegalAcceptanceRequiredException();

        exception.Code.Should().Be("LEGAL.ACCEPTANCE_REQUIRED");
        exception.HttpStatus.Should().Be(403);
    }

    [Fact]
    public void Too_many_requests_maps_to_429()
    {
        new TooManyRequestsException().HttpStatus.Should().Be(429);
    }

    [Fact]
    public void Validation_carries_field_errors_and_maps_to_422()
    {
        var errors = new List<FieldError> { new("amount", "is required") };

        var exception = new ValidationException(errors);

        exception.HttpStatus.Should().Be(422);
        exception.Code.Should().Be("DOMAIN.VALIDATION");
        exception.Errors.Should().ContainSingle().Which.Path.Should().Be("amount");
    }

    [Fact]
    public void Missing_tenant_context_has_its_own_code()
    {
        new MissingTenantContextException().Code.Should().Be("DOMAIN.MISSING_TENANT_CONTEXT");
    }

    /// <summary>
    /// The error catalogue is a public contract: the frontend maps these codes
    /// and i18n keys, so every exception must keep the exact triple.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExceptionCatalog))]
    public void Every_domain_exception_exposes_a_stable_triple(
        DomainException exception,
        string expectedCode,
        string expectedI18nKey,
        int expectedStatus)
    {
        exception.Code.Should().Be(expectedCode);
        exception.I18nKey.Should().Be(expectedI18nKey);
        exception.HttpStatus.Should().Be(expectedStatus);
        exception.Message.Should().NotBeNullOrWhiteSpace();
    }

    public static TheoryData<DomainException, string, string, int> ExceptionCatalog => new()
    {
        { new InvalidArgumentDomainException("invalid"), "DOMAIN.INVALID_ARGUMENT", "domain.invalid_argument", 400 },
        { new MissingTenantContextException(), "DOMAIN.MISSING_TENANT_CONTEXT", "domain.missing_tenant_context", 400 },
        { new NotFoundException(), "DOMAIN.NOT_FOUND", "error.not_found", 404 },
        { new ConflictException(), "DOMAIN.CONFLICT", "error.conflict", 409 },
        { new ForbiddenException(), ForbiddenException.ErrorCode, ForbiddenException.MessageKey, 403 },
        { new UnauthorizedException(), UnauthorizedException.ErrorCode, UnauthorizedException.MessageKey, 401 },
        { new PasswordChangeRequiredException(), "AUTH.PASSWORD_CHANGE_REQUIRED", "auth.password_change_required", 403 },
        { new LegalAcceptanceRequiredException(), "LEGAL.ACCEPTANCE_REQUIRED", "legal.acceptance_required", 403 },
        { new TooManyRequestsException(), "DOMAIN.TOO_MANY_REQUESTS", "error.too_many_requests", 429 },
        {
            new ValidationException([new FieldError("amount", "is required")]),
            "DOMAIN.VALIDATION",
            "error.validation",
            422
        },
    };
}
