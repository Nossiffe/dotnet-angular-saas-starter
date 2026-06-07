using Vogen;

namespace Saas.Domain.Tenancy;

[ValueObject<string>(conversions: Conversions.SystemTextJson, comparison: ComparisonGeneration.Omit)]
public partial struct Slug
{
    private static string NormalizeInput(string input) =>
        input is null ? string.Empty : input.Trim().ToLowerInvariant().Replace(' ', '-');

    private static Validation Validate(string input) =>
        string.IsNullOrWhiteSpace(input)
            ? Validation.Invalid("Le slug ne peut pas être vide.")
            : Validation.Ok;
}
