using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Saas.Api.Common;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (status, title, errors) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Validation échouée",
                (object?)ve.Errors
                    .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray(), StringComparer.Ordinal)),
            // Filet de course générique : le chemin normal (ex. invitation en double) est déjà
            // couvert en amont par une erreur ErrorOr précise et spécifique au domaine ; cette
            // branche ne s'atteint que si une contrainte d'unicité est violée après ce check
            // applicatif (race), donc sans le contexte métier pour formuler un message précis.
            Microsoft.EntityFrameworkCore.DbUpdateException due when IsUniqueViolation(due) => (
                StatusCodes.Status409Conflict,
                "Cette ressource existe déjà (conflit d'unicité).",
                (object?)null),
            Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "La ressource a été modifiée par une autre opération, réessayez.",
                (object?)null),
            _ => (StatusCodes.Status500InternalServerError, "Erreur interne du serveur", (object?)null),
        };

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
        };
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        // Corrélation logs/traces : le traceId permet de relier la réponse d'erreur aux spans OTel et aux logs.
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    // Détecte une violation de contrainte d'unicité Postgres (SQLSTATE 23505),
    // p. ex. l'index unique partiel sur les invitations en attente.
    private static bool IsUniqueViolation(Microsoft.EntityFrameworkCore.DbUpdateException exception)
        => exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
}
