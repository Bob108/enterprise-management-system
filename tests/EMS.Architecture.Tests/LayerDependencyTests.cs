using System.Reflection;
using EMS.Domain.Common;
using NetArchTest.Rules;

namespace EMS.Architecture.Tests;

/// <summary>
/// Enforces the Clean Architecture dependency rule (design §5.3). If one of these tests
/// fails, a project reference or using-directive has crossed a layer boundary — fix the
/// code, never the test.
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly WebApiAssembly = typeof(Program).Assembly;

    // EMS.Shared is deliberately allowed from Domain: it holds dependency-free primitives
    // (status enums) shared with the Blazor client (design §7.2).
    [Fact]
    public void Domain_has_no_outward_dependencies()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EMS.Application",
                "EMS.Infrastructure",
                "EMS.WebAPI",
                "EMS.BlazorUI",
                "Microsoft.EntityFrameworkCore",
                "MediatR",
                "FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful, Violations(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_hosts()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "EMS.Infrastructure",
                "EMS.WebAPI",
                "EMS.BlazorUI",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Violations(result));
    }

    [Fact]
    public void Controllers_do_not_touch_persistence_directly()
    {
        var result = Types.InAssembly(WebApiAssembly)
            .That().ResideInNamespace("EMS.WebAPI.Controllers")
            .ShouldNot().HaveDependencyOn("EMS.Infrastructure.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Violations(result));
    }

    private static string Violations(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Layer rule violated by: " + string.Join(", ", result.FailingTypeNames ?? []);
}
