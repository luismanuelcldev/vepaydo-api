using FluentAssertions;
using VepayDO.Application.Audit;
using VepayDO.Application.Auth;
using VepayDO.Application.Tenancy;
using VepayDO.Application.Tenants;
using VepayDO.Application.Tenants.Ports;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Tenants;

namespace VepayDO.Application.Tests.Tenants;

public sealed partial class TenantSettingsServiceTests
{

    private sealed class FakeTenantContextAccessor(TenantContext? context) : ITenantContextAccessor
    {
        public TenantContext? Current { get; } = context;
    }
}
