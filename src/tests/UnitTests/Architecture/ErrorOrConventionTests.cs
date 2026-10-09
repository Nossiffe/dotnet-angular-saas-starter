using System.Reflection;
using ErrorOr;
using Mediator;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed class ErrorOrConventionTests
{
    private static readonly Assembly _appAsm = typeof(Saas.Application.DependencyInjection).Assembly;
    private static readonly Assembly _apiAsm = typeof(Saas.Api.Common.GlobalExceptionHandler).Assembly;
    private static readonly Assembly _domainAsm = typeof(Saas.Domain.Products.ProductErrors).Assembly;

    // Tout ICommand<T>/IQuery<T> a un TResponse ErrorOr<>.
    [Fact]
    public void Commands_et_Queries_retournent_ErrorOr()
    {
        var violations = new List<string>();
        foreach (var type in _appAsm.GetTypes())
        {
            foreach (var itf in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var def = itf.GetGenericTypeDefinition();
                if (def != typeof(ICommand<>) && def != typeof(IQuery<>))
                {
                    continue;
                }
                var response = itf.GetGenericArguments()[0];
                var isErrorOr = response.IsGenericType
                    && response.GetGenericTypeDefinition() == typeof(ErrorOr<>);
                if (!isErrorOr)
                {
                    violations.Add($"{type.Name} -> {response.Name}");
                }
            }
        }
        violations.ShouldBeEmpty($"Use-cases hors pattern ErrorOr : {string.Join(", ", violations)}");
    }

    // Les exceptions métier supprimées ne doivent pas réapparaître.
    [Fact]
    public void Aucune_exception_metier_custom_dans_Domain()
    {
        var offenders = Types.InAssembly(_domainAsm)
            .That().Inherit(typeof(Exception))
            .GetTypes()
            .Select(t => t.Name)
            .ToList();
        offenders.ShouldBeEmpty(
            $"Exceptions custom dans Domain (utiliser ErrorOr) : {string.Join(", ", offenders)}");
    }

    // Un refus métier est une erreur ErrorOr. ValidationException reste réservée au pipeline
    // FluentValidation et aux refus d'Identity liés à un champ (docs/adr/0003).
    [Fact]
    public void ValidationException_is_thrown_only_at_the_input_boundary()
    {
        string[] allowed = ["ValidationBehavior.cs", "RegisterHandler.cs", "AcceptInvitationHandler.cs"];
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Saas.slnx")))
        {
            root = root.Parent;
        }

        root.ShouldNotBeNull();
        var throwers = Directory.EnumerateFiles(Path.Combine(root.FullName, "src", "Application"), "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("throw new ValidationException", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        throwers.ShouldNotBeEmpty(); // le test lit bien le code source
        throwers.Except(allowed, StringComparer.Ordinal).ShouldBeEmpty();
    }

    // Contrat OpenAPI->TS : toute propriété non-nullable d'un contract Api est
    // requise dans le schéma — garanti par ctor positionnel (record) ou `required`.
    [Fact]
    public void Contracts_Api_proprietes_non_nullables_requises()
    {
        var contractTypes = _apiAsm.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Saas.Api.Contracts", StringComparison.Ordinal) == true
                && t.IsClass && t.IsPublic);
        var nullability = new NullabilityInfoContext();
        var violations = new List<string>();
        foreach (var type in contractTypes)
        {
            var ctorParams = type.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is not null)
                {
                    continue;
                }
                if (!prop.PropertyType.IsValueType
                    && nullability.Create(prop).ReadState == NullabilityState.Nullable)
                {
                    continue;
                }
                var isRequired = ctorParams.Contains(prop.Name!)
                    || prop.GetCustomAttributes().Any(a =>
                        string.Equals(a.GetType().Name, "RequiredMemberAttribute", StringComparison.Ordinal));
                if (!isRequired)
                {
                    violations.Add($"{type.Name}.{prop.Name}");
                }
            }
        }
        violations.ShouldBeEmpty(
            $"Propriétés non-nullables sans required/ctor (le TS généré les rendrait optionnelles) : {string.Join(", ", violations)}");
    }

    // Les DTOs appartiennent à Contracts/Application — jamais imbriqués dans un endpoint.
    [Fact]
    public void Aucun_type_imbrique_dans_les_endpoints()
    {
        var offenders = _apiAsm.GetTypes()
            .Where(t => t.Namespace?.StartsWith("Saas.Api.Endpoints", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            .Where(n => !n.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .Select(n => n.FullName)
            .ToList();
        offenders.ShouldBeEmpty(
            $"Types imbriqués dans des endpoints (déplacer vers Contracts/Application) : {string.Join(", ", offenders)}");
    }
}
