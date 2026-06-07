using System.Reflection;
using Mediator;
using NetArchTest.Rules;
using Saas.Domain.Common;
using Saas.Domain.Products;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed class ConventionTests
{
    private static readonly Assembly _appAsm = typeof(Saas.Application.DependencyInjection).Assembly;

    // ── C2a ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Handlers_dans_Application_sont_sealed()
    {
        var result = Types.InAssembly(_appAsm)
            .That()
            .HaveNameEndingWith("Handler", StringComparison.Ordinal)
            .And()
            .AreClasses()
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Handlers non-sealed : {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // ── C2b ─────────────────────────────────────────────────────────────────
    [Fact]
    public void Implementations_de_ICommandHandler_resident_dans_Application()
    {
        // ICommandHandler<TRequest, TResponse> — martinothamar/Mediator
        var result = Types.InAssembly(_appAsm)
            .That()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Should()
            .ResideInNamespaceStartingWith("Saas.Application")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"ICommandHandler hors Application : {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Implementations_de_IQueryHandler_resident_dans_Application()
    {
        var result = Types.InAssembly(_appAsm)
            .That()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Should()
            .ResideInNamespaceStartingWith("Saas.Application")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"IQueryHandler hors Application : {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Implementations_de_INotificationHandler_resident_dans_Application()
    {
        var result = Types.InAssembly(_appAsm)
            .That()
            .ImplementInterface(typeof(INotificationHandler<>))
            .Should()
            .ResideInNamespaceStartingWith("Saas.Application")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"INotificationHandler hors Application : {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // ── C2c ─────────────────────────────────────────────────────────────────
    // Vérifie qu'aucune propriété MÉTIER des agrégats n'expose de setter public.
    // Les propriétés d'infrastructure (IAuditable, ISoftDeletable, ITenantOwned, Id)
    // sont volontairement exclues : elles sont settées par l'intercepteur EF.
    [Fact]
    public void Agregats_nont_pas_de_setters_publics_sur_les_proprietes_metier()
    {
        // Propriétés d'infrastructure exclues (requises par EF / intercepteurs)
        var infrastructureInterfaces = new[]
        {
            typeof(IAuditable),
            typeof(ISoftDeletable),
            typeof(ITenantOwned),
        };

        var typesToCheck = new[] { typeof(Product), typeof(Organization) };

        var violations = new List<string>();
        foreach (var type in typesToCheck)
        {
            // Noms de propriétés provenant des interfaces d'infrastructure
            var infraPropNames = infrastructureInterfaces
                .SelectMany(i => i.GetProperties())
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            // "Id" est hérité d'Entity<TId> et géré par EF — exclure aussi.
            infraPropNames.Add("Id");

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var prop in props)
            {
                if (infraPropNames.Contains(prop.Name))
                {
                    continue; // propriété d'infrastructure légitime
                }

                if (prop.SetMethod?.IsPublic == true)
                {
                    violations.Add($"{type.Name}.{prop.Name}");
                }
            }
        }

        violations.ShouldBeEmpty(
            $"Propriétés métier avec setter public sur les agrégats : {string.Join(", ", violations)}");
    }
}
