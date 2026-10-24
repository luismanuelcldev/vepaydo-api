using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace VepayDO.Architecture.Tests;

public sealed partial class LayeringTests
{

    [Fact]
    public void Api_assembly_is_loadable()
    {
        ApiAssembly.GetName().Name.Should().Be("VepayDO.Api");
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {

        if (assembly.GetTypes().Length == 0)
        {
            return;
        }

        TestResult result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "{0} must not depend on [{1}], but these types do: {2}",
            assembly.GetName().Name,
            string.Join(", ", forbidden),
            string.Join(", ", result.FailingTypeNames ?? new List<string>()));
    }
}
