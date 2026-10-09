using System.Reflection;
using System.Text.RegularExpressions;
using ErrorOr;
using Saas.Application.Auth;
using Saas.Domain.Tenancy;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Common;

// docs/error-codes.md est le registre des erreurs métier lu par les développeurs et les clients de
// l'API. Ce test le compare au code : une erreur ajoutée, renommée ou reformulée sans mise à jour du
// registre fait échouer la CI.
public sealed partial class ErrorRegistryTests
{
    private static readonly Assembly[] _assemblies = [typeof(OrganizationErrors).Assembly, typeof(AuthErrors).Assembly];

    [Fact]
    public void The_registry_lists_exactly_the_errors_of_the_code()
    {
        var documented = DocumentedErrors().Select(e => e.Member).Order(StringComparer.Ordinal);
        var declared = DeclaredErrors().Select(e => e.Member).Order(StringComparer.Ordinal);

        documented.ShouldBe(declared);
    }

    [Fact]
    public void Each_documented_error_has_the_type_code_and_description_of_the_code()
    {
        var declared = DeclaredErrors().ToDictionary(e => e.Member, StringComparer.Ordinal);

        foreach (var doc in DocumentedErrors())
        {
            var error = declared[doc.Member].Error;
            $"{doc.Member} : {error.Type} {error.Code} {error.Description}"
                .ShouldBe($"{doc.Member} : {doc.Type} {doc.Code} {doc.Description}");
        }
    }

    [Fact]
    public void Each_description_is_a_sentence()
    {
        DeclaredErrors().Where(e => !e.Error.Description.EndsWith('.')).Select(e => e.Member).ShouldBeEmpty();
    }

    private static IEnumerable<(string Member, Error Error)> DeclaredErrors()
        => _assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: true, IsSealed: true } && t.Name.EndsWith("Errors", StringComparison.Ordinal))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(Error))
                .Select(p => ($"{t.Name}.{p.Name}", (Error)p.GetValue(null)!)));

    private static IEnumerable<(string Member, ErrorType Type, string Code, string Description)> DocumentedErrors()
        => File.ReadLines(Path.Combine(RepoRoot(), "docs", "error-codes.md"))
            .Select(line => RegistryRow().Match(line))
            .Where(m => m.Success)
            .Select(m => (
                m.Groups["member"].Value,
                Enum.Parse<ErrorType>(m.Groups["type"].Value),
                m.Groups["code"].Value,
                m.Groups["description"].Value));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Saas.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Racine du dépôt introuvable (Saas.slnx).");
    }

    // | `OrganizationErrors.NotFound` | NotFound | `Organization.NotFound` | Organisation introuvable. |
    [GeneratedRegex(
        @"^\| `(?<member>\w+Errors\.\w+)` \| (?<type>\w+) \| `(?<code>[\w.]+)` \| (?<description>.+?) \|$",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex RegistryRow();
}
