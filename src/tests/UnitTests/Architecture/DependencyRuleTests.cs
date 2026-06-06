using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed class DependencyRuleTests
{
    private const string Application = "Saas.Application";
    private const string Infrastructure = "Saas.Infrastructure";
    private const string Api = "Saas.Api";

    private static readonly System.Reflection.Assembly DomainAsm = typeof(Domain.Common.BaseEntity).Assembly;

    [Fact]
    public void Domain_ne_depend_de_rien_dautre()
    {
        var result = Types.InAssembly(DomainAsm)
            .Should().NotHaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Application_ne_depend_pas_de_Infrastructure_ni_Api()
    {
        var result = Types.InAssembly(typeof(Saas.Application.DependencyInjection).Assembly)
            .Should().NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();
        result.IsSuccessful.ShouldBeTrue();
    }
}
