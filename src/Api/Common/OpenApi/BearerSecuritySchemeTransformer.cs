using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Saas.Api.Common.OpenApi;

/// <summary>
/// Déclare le schéma <c>http</c>/<c>bearer</c> (JWT) dans le document OpenAPI, et l'exige sur chaque
/// opération qui demande une authentification. Les routes anonymes (<c>AllowAnonymous</c> : connexion,
/// inscription, rafraîchissement…) restent sans exigence : le contrat dit ce que l'API exige vraiment.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    private const string SchemeName = "Bearer";

    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!await HasBearerSchemeAsync())
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal)
        {
            [SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
            },
        };
    }

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var anonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (anonymous || !await HasBearerSchemeAsync())
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
        });
    }

    private async Task<bool> HasBearerSchemeAsync()
        => (await authenticationSchemeProvider.GetAllSchemesAsync())
            .Any(scheme => string.Equals(scheme.Name, SchemeName, StringComparison.Ordinal));
}
