using FluentAssertions;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tests;

public sealed partial class DomainExceptionTests
{

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
