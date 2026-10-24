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

/// <summary>
/// Hosts the real HTTP pipeline (configuration, middleware order, auth,
/// authorization, problem-details) with two database-facing services replaced:
/// the tenant transaction runner and the legal acceptance reader. No PostgreSQL
/// is required, so these tests stay fast and deterministic.
/// </summary>
public class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestOrigin = "http://localhost:5173";
    public const string TestJwtAccessSecret = "test-jwt-access-secret-0123456789abcdef";
    public const string TestRefreshTokenPepper = "test-refresh-token-pepper-0123456789abcdef";
    public const string TestStorageSigningSecret = "test-storage-signing-secret-0123456789abcdef";

    public RecordingTenantTransactionRunner Runner { get; } = new();

    public FakeLegalService Legal { get; } = new();

    public FakeAuthService Auth { get; } = new();

    public FakeTenantSettingsService Settings { get; } = new();

    public FakeMemberService Members { get; } = new();

    public FakeBusinessAccountService BusinessAccounts { get; } = new();

    public FakeCustomerService Customers { get; } = new();

    public FakeChargeService Charges { get; } = new();

    public FakeReceiptService Receipts { get; } = new();

    /// <summary>
    /// Permit limit for the <c>auth</c> rate-limiting policy. The production
    /// value is 10/min, which is low enough that a test class sharing one
    /// factory would exhaust it; override in a dedicated fixture to exercise
    /// the 429 path.
    /// </summary>
    protected virtual int AuthRateLimit => 1_000;

    public string CreateAccessToken(
        Guid userId,
        Guid? tenantId = null,
        string? role = null,
        Guid? sessionId = null,
        bool mustChangePassword = false)
        => Services.GetRequiredService<ITokenService>().SignAccessToken(new AccessTokenClaims
        {
            UserId = userId,
            TenantId = tenantId,
            Role = role,
            SessionId = sessionId,
            MustChangePassword = mustChangePassword,
        });

    public HttpClient CreateAnonymousClient() => CreateClient();

    public HttpClient CreateClientWithToken(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("App:DatabaseUrl", DatabaseUrl);
        builder.UseSetting("App:JwtAccessSecret", TestJwtAccessSecret);
        builder.UseSetting("App:RefreshTokenPepper", TestRefreshTokenPepper);
        builder.UseSetting("App:AppOrigin", TestOrigin);
        builder.UseSetting("App:MailTransport", "fake");
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
            services.Replace(ServiceDescriptor.Singleton<IReceiptService>(Receipts));

            services.Replace(ServiceDescriptor.Singleton(new AppConfig
            {
                Environment = "Development",
                DatabaseUrl = DatabaseUrl,
                JwtAccessSecret = TestJwtAccessSecret,
                RefreshTokenPepper = TestRefreshTokenPepper,
                AppOrigin = TestOrigin,
                MailTransport = "fake",
                StorageProvider = "inmemory",
                StoragePublicBaseUrl = "http://localhost:5000",
                StorageSigningSecret = TestStorageSigningSecret,
                StorageMaxUploadBytes = 10000000,
                StorageSignedUrlTtlSeconds = 300,
                WorkerBatchSize = 10,
                WorkerPollIntervalSeconds = 5,
                WorkerMaxAttempts = 5,
                LegalTermsVersion = "2026-01",
                LegalPrivacyVersion = "2026-01",
            }));

            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
            services.AddScoped<IValidator<SampleRequest>, SampleRequestValidator>();
        });
    }

    // Port 59999 is deliberately closed: no PostgreSQL runs for these tests, so
    // the readiness probe fails deterministically even when a local database
    // happens to be listening on 5432.
    private const string DatabaseUrl = "postgresql://vepaydo:vepaydo@127.0.0.1:59999/vepaydo_test";
}
