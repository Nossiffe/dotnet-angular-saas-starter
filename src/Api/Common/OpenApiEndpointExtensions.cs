using Microsoft.AspNetCore.Mvc;

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
    /// Réponses renvoyées avant l'endpoint par l'authentification et l'autorisation. 401 : sans
    /// access token valide, sans corps ; avec un token dont l'adhésion ou le rôle ne correspond plus
    /// à la base, ProblemDetails <c>Auth.SessionExpired</c> (<see cref="StaleTokenAuthorizationResultHandler"/>).
    /// 403 quand la policy refuse, sans corps.
    /// </summary>
    public static TBuilder WithAuthorizationResponses<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(
            new ProducesResponseTypeMetadata(
                StatusCodes.Status401Unauthorized, typeof(ProblemDetails), ["application/problem+json"]),
            new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(void)));
    }

    public static RouteHandlerBuilder WithStandardResponses<TSuccess>(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<TSuccess>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    public static RouteHandlerBuilder WithCreatedResponses<TSuccess>(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces<TSuccess>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    public static RouteHandlerBuilder WithNoContentResponses(this RouteHandlerBuilder builder)
    {
        return builder
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
