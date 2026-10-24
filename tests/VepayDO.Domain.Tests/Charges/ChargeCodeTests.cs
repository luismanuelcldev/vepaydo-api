using FluentAssertions;

using VepayDO.Domain.Charges;
using VepayDO.Domain.Errors;

namespace VepayDO.Domain.Tests.Charges;

/// <summary>
/// Unit tests for <see cref="ChargeCode"/> (UC-DATA-03): normalization, the
/// allowed alphabet, length bounds, and deterministic generation with a supplied
/// random source.
/// </summary>
public sealed class ChargeCodeTests
{
    [Theory]
    [InlineData("  fact-001 ", "FACT-001")]
    [InlineData("abc 123", "ABC-123")]
    [InlineData("A--B", "A--B")]
    [InlineData("sol2026", "SOL2026")]
    public void Parse_normalizes_to_upper_dash_form(string input, string expected)
    {
        ChargeCode.Parse(input).Value.Should().Be(expected);
    }

    [Fact]
    public void Parse_accepts_the_maximum_length()
    {
        var max = new string('A', ChargeCode.MaxLength);

        ChargeCode.Parse(max).Value.Should().Be(max);
    }

    [Fact]
    public void Parse_rejects_an_empty_code()
    {
        var act = () => ChargeCode.Parse("   ");

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Parse_rejects_an_oversized_code()
    {
        var tooLong = new string('A', ChargeCode.MaxLength + 1);

        var act = () => ChargeCode.Parse(tooLong);

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Theory]
    [InlineData("FACT_001")]
    [InlineData("FACT/001")]
    [InlineData("FÁCT")]
    public void Parse_rejects_disallowed_characters(string input)
    {
        var act = () => ChargeCode.Parse(input);

        act.Should().Throw<InvalidArgumentDomainException>();
    }

    [Fact]
    public void Generate_prefixes_the_code_and_pads_the_suffix()
    {
        var code = ChargeCode.Generate("FAC", _ => 42);

        code.Value.Should().Be("FAC-000042");
    }

    [Fact]
    public void Generate_falls_back_to_a_default_prefix_when_invalid()
    {
        var code = ChargeCode.Generate("áé?", _ => 1);

        code.Value.Should().Be("C-000001");
    }
}
