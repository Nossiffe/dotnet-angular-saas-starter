using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Saas.Api.Common;
using Saas.Api.Contracts;
using Saas.Application.Auth.ConfirmEmail;
using Saas.Application.Auth.ForgotPassword;
using Saas.Application.Auth.Login;
using Saas.Application.Auth.Logout;
using Saas.Application.Auth.RefreshToken;
using Saas.Application.Auth.Register;
using Saas.Application.Auth.ResetPassword;

namespace Saas.Api.Endpoints;

public sealed class AuthEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/register", Register)
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", Login)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", Refresh)
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // Logout : révoque le refresh token. Réservé à l'utilisateur connecté (override du group AllowAnonymous).
        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/confirm-email", ConfirmEmail)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        // Anti-énumération : renvoie toujours 200, qu'on connaisse l'email ou non.
        group.MapPost("/forgot-password", ForgotPassword)
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/reset-password", ResetPassword)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<Results<Created<RegisterResponse>, ValidationProblem>> Register(
        RegisterCommand cmd, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var id = await sender.Send(cmd, ct);
        return TypedResults.Created($"/users/{id}", new RegisterResponse(id));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Login(
        LoginCommand cmd, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var r = await sender.Send(cmd, ct);
        return r is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new AuthResponse(r.AccessToken, r.ExpiresAt, r.RefreshToken));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Refresh(
        RefreshTokenCommand cmd, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var r = await sender.Send(cmd, ct);
        return r is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new AuthResponse(r.AccessToken, r.ExpiresAt, r.RefreshToken));
    }

    private static async Task<NoContent> Logout(
        LogoutRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        await sender.Send(new LogoutCommand(request.RefreshToken), ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok, BadRequest>> ConfirmEmail(
        ConfirmEmailRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ok = await sender.Send(new ConfirmEmailCommand(request.UserId, request.Token), ct);
        return ok ? TypedResults.Ok() : TypedResults.BadRequest();
    }

    private static async Task<Ok> ForgotPassword(
        ForgotPasswordRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Toujours 200 (anti-énumération) — le handler n'expose jamais si l'email existe.
        await sender.Send(new ForgotPasswordCommand(request.Email), ct);
        return TypedResults.Ok();
    }

    private static async Task<Ok> ResetPassword(
        ResetPasswordRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Échec → ValidationException → 400 ProblemDetails via GlobalExceptionHandler.
        await sender.Send(new ResetPasswordCommand(request.UserId, request.Token, request.NewPassword), ct);
        return TypedResults.Ok();
    }
}
