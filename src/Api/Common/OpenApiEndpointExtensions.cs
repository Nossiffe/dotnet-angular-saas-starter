namespace Saas.Api.Common;

/// <summary>
/// Réponses standards des endpoints qui retournent `result.ToHttpResult()` :
/// 200/201/204 succès ; 400 validation (ValidationProblemDetails du pipeline
/// ou ProblemDetails ErrorOr Validation) ; 404 introuvable ; 409 conflit.
/// Les groupes authentifiés ajoutent 401 et 403 (<see cref="WithAuthorizationResponses{TBuilder}"/>).
/// </summary>
public static class OpenApiEndpointExtensions
{
    /// <summary>
    /// 401 sans access token valide, 403 quand la policy refuse : réponses sans corps, renvoyées
    /// avant l'endpoint par l'authentification et l'autorisation.
    /// </summary>
    public static TBuilder WithAuthorizationResponses<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(
            new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(void)),
            new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(void)));
    }

    public static RouteHandlerBuilder WithStandardResponses<TSuccess>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .Produces<TSuccess>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public static RouteHandlerBuilder WithCreatedResponses<TSuccess>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .Produces<TSuccess>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public static RouteHandlerBuilder WithNoContentResponses(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
