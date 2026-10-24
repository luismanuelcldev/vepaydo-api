using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Http;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Charges;
using VepayDO.Application.Configuration;
using VepayDO.Application.Customers;
using VepayDO.Application.Members;
using VepayDO.Application.Receipts;
using VepayDO.Application.Tenants;
using VepayDO.Infrastructure;

namespace VepayDO.Api.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddVepayDOServices(
        this IServiceCollection services,
        AppConfig appConfig)
    {
        services.AddSingleton(appConfig);
        services.AddInfrastructure(appConfig);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IBusinessAccountService, BusinessAccountService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IChargeService, ChargeService>();
        services.AddScoped<IReceiptService, ReceiptService>();

        services.AddSingleton(new ReceiptOptions(
            appConfig.StorageMaxUploadBytes,
            appConfig.StorageSignedUrlTtlSeconds));

        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 210_000_000;
            options.ValueLengthLimit = 1024 * 1024;
            options.MultipartHeadersLengthLimit = 32 * 1024;
        });

        services.AddAuthorization(options => options.AddVepayDOPolicies());
        services.AddSingleton<IAuthorizationHandler, AccessAuthorizationHandler>();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
