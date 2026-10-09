using System.Reflection;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed class DependencyRuleTests
{
    private const string Application = "Saas.Application";
    private const string Infrastructure = "Saas.Infrastructure";
    private const string Api = "Saas.Api";

    private static readonly Assembly _domainAsm = typeof(Saas.Domain.Common.BaseEntity).Assembly;
    private static readonly Assembly _applicationAsm = typeof(Saas.Application.DependencyInjection).Assembly;

    // Assemblies que le Domain a le droit de référencer : le runtime .NET, les types valeur
    // générés par Vogen et les résultats métier ErrorOr. Aucun framework (EF, ASP.NET, Identity,
    // Mediator). Vérifié sur les références réelles de l'assembly, pas sur les espaces de noms.
    private static readonly string[] _domainAllowedAssemblies = ["System.", "netstandard", "Vogen", "ErrorOr"];

    [Fact]
    public void Domain_references_no_framework()
    {
        var references = _domainAsm.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        references.ShouldContain("ErrorOr"); // le test lit bien les références réelles

        var forbidden = references
            .Where(name => !string.Equals(name, "System", StringComparison.Ordinal)
                && !_domainAllowedAssemblies.Any(allowed => name.StartsWith(allowed, StringComparison.Ordinal)))
            .ToList();

        forbidden.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_does_not_depend_on_other_layers()
    {
        var result = Types.InAssembly(_domainAsm)
            .Should().NotHaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_depend_on_Infrastructure_Api_or_persistence_frameworks()
    {
        var result = Types.InAssembly(_applicationAsm)
            .Should().NotHaveDependencyOnAny(
                Infrastructure, Api, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_Api()
    {
        var result = Types.InAssembly(typeof(Saas.Infrastructure.DependencyInjection).Assembly)
            .Should().NotHaveDependencyOn(Api)
            .GetResult();
        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
