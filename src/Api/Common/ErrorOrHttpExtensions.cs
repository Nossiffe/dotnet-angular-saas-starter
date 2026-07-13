using ErrorOr;

namespace Saas.Api.Common;

public static class ErrorOrHttpExtensions
{
    public static IResult ToHttpResult<T>(this ErrorOr<T> result)
        => result.Match(value => Results.Ok(value), Problem);

    public static IResult ToNoContentHttpResult<T>(this ErrorOr<T> result)
        => result.Match(_ => Results.NoContent(), Problem);

    public static IResult ToCreatedHttpResult<T>(this ErrorOr<T> result, Func<T, string> uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return result.Match(value => Results.Created(uri(value), value), Problem);
    }

    /// <summary>
    /// Convertit une <see cref="Error"/> isolée (hors flux <see cref="ErrorOr{T}"/>, ex. un
    /// garde de portée tenant) en <see cref="IResult"/> via le même mapping ErrorType → HTTP.
    /// </summary>
    internal static IResult ToProblemHttpResult(this Error error) => Problem([error]);

    private static IResult Problem(List<Error> errors)
    {
        var error = errors[0];
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Results.Problem(
            statusCode: statusCode,
            title: error.Description,
            type: $"https://httpstatuses.io/{statusCode}",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code });
    }
}
