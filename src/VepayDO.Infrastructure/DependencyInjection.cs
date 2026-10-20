using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Legal;
using VepayDO.Application.Notifications;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Legal;
using VepayDO.Infrastructure.Migrations;
using VepayDO.Infrastructure.Notifications;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Security;
using VepayDO.Infrastructure.Tenancy;

namespace VepayDO.Infrastructure;

/// <summary>
/// Composition root of the Infrastructure layer. Every provider that must be
/// single per process is registered here; nothing resolves the root
/// <see cref="AppDbContext"/> outside the tenant transaction (AGENTS.md section 8.5).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<TenantContextStore>();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<ILoginAttemptStore, InMemoryLoginAttemptStore>();
        services.AddSingleton<ISessionRevocationStore, InMemorySessionRevocationStore>();
        services.AddSingleton<IPasswordResetRateLimiter, InMemoryPasswordResetRateLimiter>();

        if (string.Equals(config.MailTransport, "fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IMailSender, FakeMailSender>();
        }
        else
        {
            services.AddSingleton<IMailSender, SmtpMailSender>();
        }

        var appConnectionString = PostgresConnectionString.Normalize(config.DatabaseUrl);
        var ownerConnectionString = PostgresConnectionString.Normalize(
            string.IsNullOrWhiteSpace(config.DirectUrl) ? config.DatabaseUrl : config.DirectUrl);

        services.AddSingleton(_ => AppDataSource.Build(appConnectionString));

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>(), AppDataSource.ConfigureEnums));

        services.AddScoped<ILegalService, LegalService>();
        services.AddScoped<TxProvider>();
        services.AddScoped<ITenantTransactionRunner, TenantTransactionRunner>();
        services.AddScoped(_ => new SqlMigrationRunner(ownerConnectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IMembershipRepository, MembershipRepository>();
        services.AddScoped<ITenantFactory, TenantFactory>();
        services.AddScoped<IAuditWriter, AuditEventRepository>();

        return services;
    }
}
