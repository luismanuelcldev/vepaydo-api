using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace VepayDO.Architecture.Tests;

public sealed partial class LayeringTests
{
    private static Assembly DomainAssembly => typeof(VepayDO.Domain.Money).Assembly;

    private static Assembly ApplicationAssembly =>
        typeof(VepayDO.Application.Configuration.AppConfig).Assembly;

    private static Assembly ContractsAssembly => Assembly.Load("VepayDO.Contracts");

    private static Assembly InfrastructureAssembly => Assembly.Load("VepayDO.Infrastructure");

    private static Assembly ApiAssembly => Assembly.Load("VepayDO.Api");

    [Fact]
    public void Domain_does_not_depend_on_other_layers()
    {
        AssertNoDependency(
            DomainAssembly,
            "VepayDO.Application",
            "VepayDO.Contracts",
            "VepayDO.Infrastructure",
            "VePayDO.Api");
    }

    [Fact]
    public void Domain_does_not_depend_on_infrastructure_libraries()
    {
        AssertNoDependency(
            DomainAssembly,
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "Microsoft.AspNetCore",
            "StackExchange.Redis",
            "MediatR",
            "Amazon",
            "Serilog",
            "FirebaseAdmin");
    }

    [Fact]
    public void Application_does_not_depend_on_outer_layers()
    {
        AssertNoDependency(ApplicationAssembly, "VepayDO.Infrastructure", "VePayDO.Api");
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_libraries()
    {
        AssertNoDependency(
            ApplicationAssembly,
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "StackExchange.Redis",
            "Amazon",
            "FirebaseAdmin");
    }

    [Fact]
    public void Contracts_does_not_depend_on_inner_or_outer_implementation_layers()
    {
        AssertNoDependency(
            ContractsAssembly,
            "VepayDO.Application",
            "VepayDO.Infrastructure",
            "VePayDO.Api");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_api()
    {
        AssertNoDependency(InfrastructureAssembly, "VePayDO.Api");
    }
}
