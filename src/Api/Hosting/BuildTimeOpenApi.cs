using System.Reflection;

namespace Saas.Api.Hosting;

/// <summary>Génération d'openapi.json au build, sans base ni configuration réelle (ARCHITECTURE.md § Contrat OpenAPI).</summary>
internal static class BuildTimeOpenApi
{
    public static bool IsRunning { get; } = string.Equals(
        Assembly.GetEntryAssembly()?.GetName().Name,
        "GetDocument.Insider",
        StringComparison.Ordinal);

    /// <summary>Valeurs factices qui passent la validation des options ; la DSN sans mot de passe n'ouvre aucune connexion.</summary>
    public static void AddPlaceholderConfiguration(this ConfigurationManager configuration)
    {
        configuration["ConnectionStrings:Default"] = "Host=build;Database=build;Username=build";
        configuration["Database:MigrateOnStartup"] = "false";
        configuration["Jwt:Issuer"] = "build";
        configuration["Jwt:Audience"] = "build";
        configuration["Jwt:SigningKey"] = new string('x', 32);
        configuration["App:BaseUrl"] = "https://build.local";
        configuration["Email:Host"] = "build";
        configuration["Email:Port"] = "25";
        configuration["Email:FromAddress"] = "build@build.local";
    }
}
