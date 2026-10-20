using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Kernel.Http;

/// <summary>
/// Validates every bound action argument with the registered FluentValidation
/// validators before the action runs, and fails fast with the stable
/// <c>DOMAIN.VALIDATION</c> problem-details error (AGENTS.md section 3.3).
/// Model-state failures (a value that cannot be bound) are reported the same
/// way, so one shape covers both.
/// </summary>
public sealed partial class ValidationActionFilter(
    IServiceProvider serviceProvider,
    ILogger<ValidationActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var errors = CollectModelStateErrors(context);
        if (errors.Count == 0)
        {
            errors = await CollectValidatorErrorsAsync(context).ConfigureAwait(false);
        }

        if (errors.Count > 0)
        {
            throw new VepayDO.Domain.Errors.ValidationException(errors);
        }

        await next().ConfigureAwait(false);
    }

    private static List<FieldError> CollectModelStateErrors(ActionExecutingContext context)
    {
        var errors = new List<FieldError>();
        if (context.ModelState.IsValid)
        {
            return errors;
        }

        foreach (var entry in context.ModelState)
        {
            foreach (var error in entry.Value?.Errors ?? [])
            {
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "is invalid"
                    : error.ErrorMessage;
                errors.Add(new FieldError(string.IsNullOrEmpty(entry.Key) ? "(root)" : entry.Key, message));
            }
        }

        return errors;
    }

    private async Task<List<FieldError>> CollectValidatorErrorsAsync(ActionExecutingContext context)
    {
        var errors = new List<FieldError>();

        foreach (var argument in context.ActionArguments)
        {
            if (argument.Value is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.Value.GetType());
            foreach (var service in serviceProvider.GetServices(validatorType))
            {
                if (service is not IValidator validator)
                {
                    continue;
                }

                var result = await validator
                    .ValidateAsync(new ValidationContext<object>(argument.Value), context.HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                if (result.IsValid)
                {
                    continue;
                }

                foreach (var failure in result.Errors)
                {
                    errors.Add(new FieldError(
                        string.IsNullOrEmpty(failure.PropertyName) ? "(root)" : failure.PropertyName,
                        failure.ErrorMessage));
                }
            }
        }

        if (errors.Count > 0)
        {
            LogValidationFailed(errors.Count);
        }

        return errors;
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Debug,
        Message = "Request validation failed with {ErrorCount} field errors")]
    private partial void LogValidationFailed(int errorCount);
}
