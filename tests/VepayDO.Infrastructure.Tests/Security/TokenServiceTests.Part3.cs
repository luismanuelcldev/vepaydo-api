using System.Text;
using FluentAssertions;
using VepayDO.Application.Configuration;
using VepayDO.Application.Security;
using VepayDO.Domain.Errors;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Security;

namespace VepayDO.Infrastructure.Tests.Security;

public sealed partial class TokenServiceTests
{

    [Fact]
    public void Refresh_tokens_are_opaque_and_base64url()
    {
        var issued = Service().IssueRefreshToken();

        issued.Token.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        issued.Hash.Should().HaveCount(32);
    }

    [Fact]
    public void Refresh_token_hash_is_deterministic_and_depends_on_the_pepper()
    {
        var service = Service();
        var issued = service.IssueRefreshToken();

        service.HashRefreshToken(issued.Token).Should().BeEquivalentTo(issued.Hash);

        var peppered = new TokenService(
            Config() with { RefreshTokenPepper = new string('c', 32) },
            new SystemClock());

        peppered.HashRefreshToken(issued.Token).Should().NotBeEquivalentTo(issued.Hash);
    }

    [Fact]
    public void Hash_comparison_rejects_different_lengths_and_differs_byte_wise()
    {
        var service = Service();
        var a = service.IssueRefreshToken().Hash;
        var b = service.IssueRefreshToken().Hash;

        service.HashesMatch(a, a).Should().BeTrue();
        service.HashesMatch(a, b).Should().BeFalse();
        service.HashesMatch(a, [1, 2, 3]).Should().BeFalse();
    }

    private static string DecodeSegment(string base64url)
    {
        var base64 = base64url.Replace('-', '+').Replace('_', '/');
        var padded = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("Invalid base64url length"),
        };

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }
}
