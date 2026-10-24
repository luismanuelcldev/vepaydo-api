using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VepayDO.Api.Tests.Probe;
using VepayDO.Application.Auth;
using VepayDO.Application.BusinessAccounts;
using VepayDO.Application.Charges;
using VepayDO.Application.Configuration;
using VepayDO.Application.Customers;
using VepayDO.Application.Legal;
using VepayDO.Application.Members;
using VepayDO.Application.Receipts;
using VepayDO.Application.Security;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants;

namespace VepayDO.Api.Tests.Fixtures;

public partial class ApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("App:DatabaseUrl", DatabaseUrl);
        builder.UseSetting("App:JwtAccessSecret", TestJwtAccessSecret);
        builder.UseSetting("App:RefreshTokenPepper", TestRefreshTokenPepper);
        builder.UseSetting("App:AppOrigin", TestOrigin);
        builder.UseSetting("App:MailTransport", "fake");
        builder.UseSetting("App:StorageProvider", "inmemory");
        builder.UseSetting("App:StoragePublicBaseUrl", "http://localhost:5000");
        builder.UseSetting("App:StorageSigningSecret", TestStorageSigningSecret);
        builder.UseSetting("App:AuthRateLimit", AuthRateLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("App:AuthStrictRateLimit", AuthRateLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));

        builder.ConfigureAppConfiguration((context, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:Environment"] = "Development",
                ["App:DatabaseUrl"] = DatabaseUrl,
                ["App:JwtAccessSecret"] = TestJwtAccessSecret,
                ["App:RefreshTokenPepper"] = TestRefreshTokenPepper,
                ["App:AppOrigin"] = TestOrigin,
                ["App:MailTransport"] = "fake",
                ["App:AuthRateLimit"] = AuthRateLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["App:AuthStrictRateLimit"] = AuthRateLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["App:LegalTermsVersion"] = "2026-01",
                ["App:LegalPrivacyVersion"] = "2026-01",
                ["App:StorageProvider"] = "inmemory",
                ["App:StoragePublicBaseUrl"] = "http://localhost:5000",
                ["App:StorageSigningSecret"] = TestStorageSigningSecret,
                ["App:StorageMaxUploadBytes"] = "10000000",
                ["App:StorageSignedUrlTtlSeconds"] = "300",
                ["App:WorkerBatchSize"] = "10",
                ["App:WorkerPollIntervalSeconds"] = "5",
                ["App:WorkerMaxAttempts"] = "5",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ITenantTransactionRunner>(Runner));
            services.Replace(ServiceDescriptor.Singleton(Runner));
            services.Replace(ServiceDescriptor.Singleton<ILegalService>(Legal));
            services.Replace(ServiceDescriptor.Singleton<IAuthService>(Auth));
            services.Replace(ServiceDescriptor.Singleton<ITenantSettingsService>(Settings));
            services.Replace(ServiceDescriptor.Singleton<IMemberService>(Members));
            services.Replace(ServiceDescriptor.Singleton<IBusinessAccountService>(BusinessAccounts));
            services.Replace(ServiceDescriptor.Singleton<ICustomerService>(Customers));
            services.Replace(ServiceDescriptor.Singleton<IChargeService>(Charges));
            services.Replace(ServiceDescriptor.Singleton<IReceiptService>(Receipts));

            services.Replace(ServiceDescriptor.Singleton(new AppConfig
            {
                Environment = "Development", DatabaseUrl = DatabaseUrl, JwtAccessSecret = TestJwtAccessSecret,
                RefreshTokenPepper = TestRefreshTokenPepper, AppOrigin = TestOrigin, MailTransport = "fake",
                StorageProvider = "inmemory", StoragePublicBaseUrl = "http://localhost:5000",
                StorageSigningSecret = TestStorageSigningSecret, StorageMaxUploadBytes = 10000000,
                StorageSignedUrlTtlSeconds = 300, WorkerBatchSize = 10, WorkerPollIntervalSeconds = 5,
                WorkerMaxAttempts = 5, LegalTermsVersion = "2026-01", LegalPrivacyVersion = "2026-01",
            }));

            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
            services.AddScoped<IValidator<SampleRequest>, SampleRequestValidator>();
        });
    }
}
