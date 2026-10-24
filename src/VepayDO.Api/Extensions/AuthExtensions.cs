using VepayDO.Api.Kernel.Http;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VepayDO.Api.Kernel.Auth;
using VepayDO.Application.Configuration;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;

namespace VepayDO.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddVepayDOAuthentication(
        this IServiceCollection services,
        AppConfig appConfig)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
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
                        var sessionId = context.Principal.SessionIdOf();
                        if (sessionId is null) return;

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

        return services;
    }
}
