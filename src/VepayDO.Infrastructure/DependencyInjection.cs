using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Amazon;
using Amazon.S3;
using Npgsql;

using VepayDO.Application.Audit;
using VepayDO.Application.Auth.Ports;
using VepayDO.Application.BusinessAccounts.Ports;
using VepayDO.Application.Charges.Ports;
using VepayDO.Application.Configuration;
using VepayDO.Application.Customers.Ports;
using VepayDO.Application.Legal;
using VepayDO.Application.Notifications;
using VepayDO.Application.Receipts.Ports;
using VepayDO.Application.Security;
using VepayDO.Application.Storage;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Charges;
using VepayDO.Infrastructure.Images;
using VepayDO.Infrastructure.Legal;
using VepayDO.Infrastructure.Migrations;
using VepayDO.Infrastructure.Notifications;
using VepayDO.Infrastructure.Persistence;
using VepayDO.Infrastructure.Persistence.Repositories;
using VepayDO.Infrastructure.Security;
using VepayDO.Infrastructure.Storage;
using VepayDO.Infrastructure.Storage.InMemory;
using VepayDO.Infrastructure.Storage.S3;
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
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
        services.AddScoped<IBankRepository, BankRepository>();
        services.AddScoped<IBusinessAccountRepository, BusinessAccountRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IChargeRepository, ChargeRepository>();
        services.AddSingleton<IChargeCodeSource, ChargeCodeSource>();
        services.AddScoped<IAuditWriter, AuditEventRepository>();

        AddStorage(services, config);

        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IReceiptJobStore, ReceiptJobStore>();
        services.AddScoped<IReceiptJobReader>(sp => (ReceiptJobStore)sp.GetRequiredService<IReceiptJobStore>());
        services.AddSingleton<IReceiptImageProcessor, SkiaReceiptImageProcessor>();

        return services;
    }

    /// <summary>
    /// Registers the storage family chosen by the configuration: the in-memory
    /// one for a local run and the tests, S3-compatible for a deployment
    /// (M5.4). The application layer never learns which one is active.
    /// </summary>
    private static void AddStorage(IServiceCollection services, AppConfig config)
    {
        if (string.Equals(config.StorageProvider, StorageProviders.S3, StringComparison.OrdinalIgnoreCase))
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = config.S3Endpoint,
                ForcePathStyle = config.S3ForcePathStyle,
                AuthenticationRegion = config.S3Region,
            };

            services.AddSingleton<IAmazonS3>(_ => string.IsNullOrWhiteSpace(config.S3AccessKey)
                ? new AmazonS3Client(s3Config)
                : new AmazonS3Client(config.S3AccessKey, config.S3SecretKey, s3Config));

            services.AddSingleton<IStorageToolkitFactory>(
                sp => new S3StorageToolkitFactory(
                    sp.GetRequiredService<IAmazonS3>(),
                    config.S3Bucket ?? string.Empty));
        }
        else
        {
            services.AddSingleton(_ => new StorageUrlSigner(config.StorageSigningSecret));
            services.AddSingleton<IStorageToolkitFactory>(
                sp => new InMemoryStorageToolkitFactory(
                    sp.GetRequiredService<StorageUrlSigner>(),
                    config.StoragePublicBaseUrl,
                    sp.GetRequiredService<IClock>()));
        }

        services.AddSingleton<IStorageToolkit>(sp => sp.GetRequiredService<IStorageToolkitFactory>().Create());
    }
}