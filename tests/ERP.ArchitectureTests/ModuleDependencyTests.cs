using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace ERP.ArchitectureTests;

public class ModuleDependencyTests
{
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(Modules.Identity.DependencyInjection).Assembly,
        typeof(Modules.Tenancy.DependencyInjection).Assembly,
        typeof(Modules.Accounting.DependencyInjection).Assembly,
        typeof(Modules.Treasury.DependencyInjection).Assembly,
        typeof(Modules.Inventory.DependencyInjection).Assembly,
        typeof(Modules.Purchasing.DependencyInjection).Assembly,
        typeof(Modules.Sales.DependencyInjection).Assembly,
        typeof(Modules.Parties.DependencyInjection).Assembly,
        typeof(Modules.Payroll.DependencyInjection).Assembly,
        typeof(Modules.Reporting.DependencyInjection).Assembly,
        typeof(Modules.Subscriptions.DependencyInjection).Assembly,
        typeof(Modules.Audit.DependencyInjection).Assembly,
    ];

    [Fact]
    public void BuildingBlocks_ShouldNotDependOn_ApiOrModules()
    {
        var buildingBlocksAssembly = typeof(BuildingBlocks.Results.Result).Assembly;

        var result = Types.InAssembly(buildingBlocksAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ERP.Api",
                "ERP.Modules.Identity",
                "ERP.Modules.Tenancy",
                "ERP.Modules.Accounting",
                "ERP.Modules.Treasury",
                "ERP.Modules.Inventory",
                "ERP.Modules.Purchasing",
                "ERP.Modules.Sales",
                "ERP.Modules.Parties",
                "ERP.Modules.Payroll",
                "ERP.Modules.Reporting",
                "ERP.Modules.Subscriptions",
                "ERP.Modules.Audit")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("BuildingBlocks must be completely decoupled from Api and all business modules.");
    }

    [Theory]
    [InlineData("ERP.Modules.Identity")]
    [InlineData("ERP.Modules.Tenancy")]
    [InlineData("ERP.Modules.Accounting")]
    [InlineData("ERP.Modules.Treasury")]
    [InlineData("ERP.Modules.Inventory")]
    [InlineData("ERP.Modules.Purchasing")]
    [InlineData("ERP.Modules.Sales")]
    [InlineData("ERP.Modules.Parties")]
    [InlineData("ERP.Modules.Payroll")]
    [InlineData("ERP.Modules.Reporting")]
    [InlineData("ERP.Modules.Subscriptions")]
    [InlineData("ERP.Modules.Audit")]
    public void Module_ShouldNotDependOn_AnyOtherModule(string targetModuleName)
    {
        var allModuleNames = new[]
        {
            "ERP.Modules.Identity",
            "ERP.Modules.Tenancy",
            "ERP.Modules.Accounting",
            "ERP.Modules.Treasury",
            "ERP.Modules.Inventory",
            "ERP.Modules.Purchasing",
            "ERP.Modules.Sales",
            "ERP.Modules.Parties",
            "ERP.Modules.Payroll",
            "ERP.Modules.Reporting",
            "ERP.Modules.Subscriptions",
            "ERP.Modules.Audit"
        };

        var otherModuleNames = allModuleNames.Where(m => m != targetModuleName).ToArray();
        var targetAssembly = ModuleAssemblies.First(a => a.GetName().Name == targetModuleName);

        var result = Types.InAssembly(targetAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherModuleNames)
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"Module '{targetModuleName}' must not depend directly on other modules.");
    }

    [Fact]
    public void DomainLayers_ShouldNotDependOn_InfrastructureLayers()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceEndingWith("Domain")
                .ShouldNot()
                .HaveDependencyOnAny(
                    $"{assembly.GetName().Name}.Infrastructure",
                    "Microsoft.EntityFrameworkCore",
                    "Npgsql.EntityFrameworkCore.PostgreSQL")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Domain layer in '{assembly.GetName().Name}' must be clean and not depend on Infrastructure or EF Core.");
        }
    }

    [Fact]
    public void DomainLayers_ShouldNotDependOn_ApplicationLayers()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceEndingWith("Domain")
                .ShouldNot()
                .HaveDependencyOn($"{assembly.GetName().Name}.Application")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Domain layer in '{assembly.GetName().Name}' must not depend on Application layer.");
        }
    }

    [Fact]
    public void ApplicationLayers_ShouldNotDependOn_InfrastructureLayers()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespaceEndingWith("Application")
                .ShouldNot()
                .HaveDependencyOn($"{assembly.GetName().Name}.Infrastructure")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"Application layer in '{assembly.GetName().Name}' must not depend directly on Infrastructure layer.");
        }
    }

    [Fact]
    public void ApiControllers_ShouldResideIn_ControllersNamespace()
    {
        var apiAssembly = typeof(Program).Assembly;

        var result = Types.InAssembly(apiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .ResideInNamespace("ERP.Api.Controllers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("All API controllers must reside inside ERP.Api.Controllers namespace.");
    }
}
