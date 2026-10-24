using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Json;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Api.Kernel.Health;
using VepayDO.Api.Kernel.Http;
using VepayDO.Api.Kernel.Logging;
using VepayDO.Api.Kernel.Routing;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Charges;
using VepayDO.Application.Customers;
using VepayDO.Application.Members;
using VepayDO.Application.Receipts;
using VepayDO.Application.Security;
using VepayDO.Application.Tenants;
using VepayDO.Domain.Errors;
using VepayDO.Infrastructure;
using VepayDO.Infrastructure.Configuration;
using VepayDO.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    // Never let a sensitive field (sender, reference, token, password, email)
    // reach a sink, even if someone logs a whole object (AGENTS.md section 13).
    .Destructure.With<RedactingDestructuringPolicy>()
    .WriteTo.Console(new JsonFormatter(renderMessage: false)));

// Configuration is bound and validated once, before any service is built, so a
// misconfigured process fails at boot instead of on the first request.
var appConfig = AppConfigFactory.Create(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.AddSingleton(appConfig);
builder.Services.AddInfrastructure(appConfig);

// Application-layer services that orchestrate the repositories live here so the
// Application assembly stays free of any dependency-injection package.
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<IBusinessAccountService, BusinessAccountService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IChargeService, ChargeService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();

// The upload limits are configuration-driven so a deployment can retune them
// without a rebuild (M5.4).
builder.Services.AddSingleton(new ReceiptOptions(
    appConfig.StorageMaxUploadBytes,
    appConfig.StorageSignedUrlTtlSeconds));

// A receipt upload carries up to 20 files of 10 MB, so the default 128 MB
// multipart limit is not enough (INTEGRATION.md section 5).
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 210_000_000;
    options.ValueLengthLimit = 1024 * 1024;
    options.MultipartHeadersLengthLimit = 32 * 1024;
});

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationActionFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Enums travel as their stable UPPER_SNAKE names ("OWNER", "CONFIRMED"),
        // never as ordinals, so the API contract survives enum reordering
        // (AGENTS.md section 4.4).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddMvcOptions(options =>
    {
        options.Conventions.Add(new RoutePrefixConvention("api/v1"));
    });

// Fixed-window, per-IP throttling for the anonymous auth endpoints (section 10).
// Limits are configuration-driven so a deployment (or a test host) can retune
// them without a rebuild; production defaults are 10/min and 5/min.
var authRateLimit = builder.Configuration.GetValue("App:AuthRateLimit", 10);
var authStrictRateLimit = builder.Configuration.GetValue("App:AuthStrictRateLimit", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => FixedWindow(context, permitLimit: authRateLimit));
    options.AddPolicy("auth-strict", context => FixedWindow(context, permitLimit: authStrictRateLimit));
    options.OnRejected = async (context, cancellationToken) => await ProblemJson.WriteAsync(
        context.HttpContext,
        StatusCodes.Status429TooManyRequests,
        code: "DOMAIN.TOO_MANY_REQUESTS",
        i18nKey: "error.too_many_requests",
        detail: "Too many requests",
        errors: null,
        cancellationToken).ConfigureAwait(false);
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

// Validators live beside the controllers they serve and are discovered here, so
// a new module only adds its validator class (AGENTS.md section 3.3).
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddAuthorization(options => options.AddVepayDOPolicies());
builder.Services.AddSingleton<IAuthorizationHandler, AccessAuthorizationHandler>();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

if (!builder.Environment.IsProduction())
{
    builder.Services.AddOpenApi();
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claims are read back exactly as issued: no inbound claim-type mapping.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = appConfig.AppOrigin,
            ValidateAudience = true,
            ValidAudience = appConfig.AppOrigin,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appConfig.JwtAccessSecret)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = TokenConstants.SubjectClaim,
            RoleClaimType = TokenConstants.RoleClaim,
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                // Revocation is immediate: the access token carries its session
                // id and a revoked session fails the request (AGENTS.md 9.9).
                var sessionId = context.Principal.SessionIdOf();
                if (sessionId is null)
                {
                    return;
                }

                var revocationStore = context.HttpContext.RequestServices
                    .GetRequiredService<ISessionRevocationStore>();

                if (await revocationStore
                        .IsRevokedAsync(sessionId.Value, context.HttpContext.RequestAborted)
                        .ConfigureAwait(false))
                {
                    context.Fail("The session has been revoked");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ProblemJson.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    UnauthorizedException.ErrorCode,
                    UnauthorizedException.MessageKey,
                    detail: "Authentication required",
                    errors: null,
                    context.HttpContext.RequestAborted).ConfigureAwait(false);
            },
            OnForbidden = async context => await ProblemJson.WriteAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                ForbiddenException.ErrorCode,
                ForbiddenException.MessageKey,
                detail: "Operation not allowed",
                errors: null,
                context.HttpContext.RequestAborted).ConfigureAwait(false),
        };
    });

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

// Endpoints that produce a bare status code (an unmatched route, a wrong verb)
// answer with the same problem-details document as everything else.
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    await ProblemJson.WriteAsync(
        httpContext,
        httpContext.Response.StatusCode,
        code: null,
        i18nKey: null,
        detail: null,
        errors: null,
        httpContext.RequestAborted).ConfigureAwait(false);
});

app.UseRouting();

// Origin check → authentication → request log enrichment → account gates →
// authorization → tenant scope.
app.UseMiddleware<OriginGuardMiddleware>();
app.UseAuthentication();
app.UseMiddleware<RequestLogEnrichmentMiddleware>();
app.UseMiddleware<AccountGatesMiddleware>();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi("/api/v1/docs/openapi.json");
}

app.UseRateLimiter();
app.MapControllers();
app.MapHealthEndpoints();

await app.RunAsync().ConfigureAwait(false);

static RateLimitPartition<string> FixedWindow(HttpContext context, int permitLimit)
{
    var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(
        clientKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        });
}

/// <summary>Exposed so the integration tests can host the real pipeline.</summary>
public partial class Program;
