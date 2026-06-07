using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Contracts;
using Saas.Application.Auth.Login;
using Saas.Application.Auth.RefreshToken;
using Saas.Application.Auth.Register;

namespace Saas.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/register", RegisterHandler)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginHandler)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshHandler)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<Results<Created<RegisterResponse>, ValidationProblem>> RegisterHandler(
        RegisterCommand cmd,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var id = await sender.Send(cmd, ct);
        return TypedResults.Created($"/users/{id}", new RegisterResponse(id));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginHandler(
        LoginCommand cmd,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var r = await sender.Send(cmd, ct);
        return r is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new AuthResponse(r.AccessToken, r.ExpiresAt, r.RefreshToken));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> RefreshHandler(
        RefreshTokenCommand cmd,
        ISender sender,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var r = await sender.Send(cmd, ct);
        return r is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new AuthResponse(r.AccessToken, r.ExpiresAt, r.RefreshToken));
    }
}
