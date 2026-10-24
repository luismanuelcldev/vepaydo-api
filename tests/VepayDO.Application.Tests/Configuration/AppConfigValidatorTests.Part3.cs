using FluentAssertions;
using VepayDO.Application.Configuration;
using VepayDO.Domain.Errors;

namespace VepayDO.Application.Tests.Configuration;

public sealed partial class AppConfigValidatorTests
{

    [Fact]
    public void Default_brand_name_is_vepaydo()
    {
        new AppConfig().BrandName.Should().Be("VepayDO");
    }
}
