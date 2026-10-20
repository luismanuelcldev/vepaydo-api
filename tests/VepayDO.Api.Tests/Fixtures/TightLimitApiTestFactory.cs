using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

/// <summary>
/// A factory with a two-per-minute auth permit limit, so the 429 path can be
/// exercised without polluting the shared fixture used elsewhere.
/// </summary>
public sealed class TightLimitApiTestFactory : ApiTestFactory
{
    protected override int AuthRateLimit => 2;
}
