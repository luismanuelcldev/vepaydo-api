using VepayDO.Api.Tests.Fixtures;

namespace VepayDO.Api.Tests;

public sealed class TightLimitApiTestFactory : ApiTestFactory
{
    protected override int AuthRateLimit => 2;
}
