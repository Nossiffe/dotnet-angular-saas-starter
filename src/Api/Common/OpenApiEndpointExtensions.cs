namespace Saas.Api.Common;

/// <summary>
/// Réponses standards des endpoints qui retournent `result.ToHttpResult()` :
/// 200/201/204 succès ; 400 validation (ValidationProblemDetails du pipeline
/// ou ProblemDetails ErrorOr Validation) ; 404 introuvable ; 409 conflit.
/// 401/403/429 viennent de RequireAuthorization / rate limiting.
/// </summary>
public static class OpenApiEndpointExtensions
{
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
