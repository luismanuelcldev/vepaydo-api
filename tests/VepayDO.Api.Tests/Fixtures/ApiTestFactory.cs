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

public partial class ApiTestFactory : WebApplicationFactory<Program>
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
}
