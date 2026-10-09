using System.Reflection;
using System.Text.Json;
using NetArchTest.Rules;
using Saas.Application.Common.Events;
using Saas.Domain.Common;
using Saas.Infrastructure.Persistence.Outbox;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed class ConventionTests
{
    private static readonly Assembly _domainAsm = typeof(IDomainEvent).Assembly;
    private static readonly Assembly _appAsm = typeof(Saas.Application.DependencyInjection).Assembly;

    private static readonly Type[] _domainEvents = [.. _domainAsm.GetTypes()
        .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })];

    public static TheoryData<Type> DomainEvents => [.. _domainEvents];

    [Fact]
    public void Handlers_in_Application_are_sealed()
    {
        var result = Types.InAssembly(_appAsm)
            .That().HaveNameEndingWith("Handler", StringComparison.Ordinal).And().AreClasses()
            .Should().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    // Les agrégats et leurs entités ne changent d'état que par leurs méthodes : aucune propriété
    // publique n'a de setter public, y compris le tenant, l'audit et la suppression logique.
    [Fact]
    public void Domain_entities_have_no_public_setter()
    {
        var violations = _domainAsm.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && DerivesFromGeneric(t, typeof(Entity<>)))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        violations.ShouldBeEmpty();
    }

    // Un event sans handler serait écrit dans l'outbox pour rien.
    [Theory]
    [MemberData(nameof(DomainEvents))]
    public void Every_domain_event_has_a_handler(Type domainEvent)
    {
        var handlerInterface = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent);
        _appAsm.GetTypes().ShouldContain(t => t.IsClass && handlerInterface.IsAssignableFrom(t));
    }

    // L'outbox stocke chaque event en JSON puis le relit : un event qui ne survit pas à l'aller-retour
    // échouerait seulement en production, au premier traitement.
    [Theory]
    [MemberData(nameof(DomainEvents))]
    public void Every_domain_event_survives_the_outbox_serialization(Type domainEvent)
    {
        var original = CreateSample(domainEvent);

        var json = JsonSerializer.Serialize(original, domainEvent, OutboxJson.Options);
        var roundTripped = JsonSerializer.Deserialize(json, domainEvent, OutboxJson.Options);

        roundTripped.ShouldBe(original);
    }

    private static bool DerivesFromGeneric(Type type, Type generic)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == generic)
            {
                return true;
            }
        }

        return false;
    }

    // Instance d'un event record, construite par son constructeur principal avec des valeurs non
    // triviales pour chaque paramètre (Guid, chaîne, énumération, identifiant Vogen).
    private static object CreateSample(Type type)
    {
        var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(p => SampleValue(p.ParameterType)).ToArray();
        return constructor.Invoke(arguments);
    }

    private static object SampleValue(Type type)
    {
        if (type == typeof(Guid))
        {
            return Guid.CreateVersion7();
        }

        if (type == typeof(string))
        {
            return "sample@saas.local";
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().Last();
        }

        var from = type.GetMethod("From", BindingFlags.Public | BindingFlags.Static, [typeof(Guid)])
            ?? throw new InvalidOperationException($"Pas de valeur d'exemple pour {type.Name}.");
        return from.Invoke(null, [Guid.CreateVersion7()])!;
    }
}
