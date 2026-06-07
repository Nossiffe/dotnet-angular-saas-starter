using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Saas.Api.Common.OpenApi;

/// <summary>
/// Ajoute un security scheme <c>http</c>/<c>bearer</c> (JWT) au document OpenAPI et l'applique en exigence
/// sur toutes les opérations, de sorte que le bouton « Authorize » apparaisse dans Scalar/Swagger UI et que
/// les endpoints sécurisés deviennent exécutables. Activé seulement si un schéma « Bearer » est enregistré.
/// </summary>
/// <remarks>
/// API .NET 10 validée via la doc Microsoft « Customize OpenAPI documents » (aspnetcore-10.0) :
/// <c>IOpenApiDocumentTransformer.TransformAsync(OpenApiDocument, OpenApiDocumentTransformerContext, CancellationToken)</c>,
/// types <c>Microsoft.OpenApi</c> (OpenApiSecurityScheme/OpenApiComponents/OpenApiSecurityRequirement/OpenApiSecuritySchemeReference).
/// </remarks>
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!authenticationSchemes.Any(authScheme => string.Equals(authScheme.Name, "Bearer", StringComparison.Ordinal)))
        {
            return;
        }

        // Security scheme au niveau du document.
        var securitySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal)
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
            },
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = securitySchemes;

        // Exigence Bearer appliquée à chaque opération (référence le scheme du document).
        var operations = document.Paths.Values
            .Where(path => path.Operations is not null)
            .SelectMany(path => path.Operations!.Values);
        foreach (var operation in operations)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
            });
        }
    }
}
