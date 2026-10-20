using FluentAssertions;
using VepayDO.Domain.Text;

namespace VepayDO.Domain.Tests;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("José Ramírez", "jose ramirez")]
    [InlineData("  MARÍA   de los Ángeles ", "maria de los angeles")]
    [InlineData("O'Brien & Sons, S.R.L.", "o brien sons s r l")]
    [InlineData("Ñandú", "nandu")]
    public void Normalizes_names(string input, string expected)
    {
        TextNormalizer.NormalizeName(input).Should().Be(expected);
    }

    [Fact]
    public void Rejects_null()
    {
        Action act = () => TextNormalizer.NormalizeName(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
