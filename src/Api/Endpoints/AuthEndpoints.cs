using Mediator;
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
        // Groupe de base : rate-limiting auth partagé. Chaque route gère son propre niveau d'accès.
        var group = app.MapGroup("/auth").WithTags("Auth").RequireRateLimiting("auth");

        group.MapPost("/register", Register)
            .AllowAnonymous()
            .WithCreatedResponses<RegisterResponse>();

        group.MapPost("/login", Login)
            .AllowAnonymous()
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", Refresh)
            .AllowAnonymous()
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Logout : révoque le refresh token. Réservé à l'utilisateur connecté — pas d'AllowAnonymous ici.
        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/confirm-email", ConfirmEmail)
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // Anti-énumération : renvoie toujours 200, qu'on connaisse l'email ou non.
        group.MapPost("/forgot-password", ForgotPassword)
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/reset-password", ResetPassword)
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> Register(RegisterCommand cmd, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var result = await sender.Send(cmd, ct);
        return result.Match(
            id => Results.Created($"/users/{id}", new RegisterResponse(id)),
            _ => result.ToHttpResult());
    }

    private static async Task<IResult> Login(LoginCommand cmd, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var result = await sender.Send(cmd, ct);
        return result.Match(
            auth => TypedResults.Ok(new AuthResponse(auth.AccessToken, auth.ExpiresAt, auth.RefreshToken)) as IResult,
            _ => result.ToHttpResult());
    }

    private static async Task<IResult> Refresh(RefreshTokenCommand cmd, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        var result = await sender.Send(cmd, ct);
        return result.Match(
            auth => TypedResults.Ok(new AuthResponse(auth.AccessToken, auth.ExpiresAt, auth.RefreshToken)) as IResult,
            _ => result.ToHttpResult());
    }

    private static async Task<IResult> Logout(LogoutRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.Send(new LogoutCommand(request.RefreshToken), ct);
        return result.Match(_ => Results.NoContent(), _ => result.ToHttpResult());
    }

    private static async Task<IResult> ConfirmEmail(ConfirmEmailRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.Send(new ConfirmEmailCommand(request.UserId, request.Token), ct);
        return result.Match(_ => Results.Ok(), _ => result.ToHttpResult());
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Toujours 200 (anti-énumération) — le handler n'expose jamais si l'email existe.
        var result = await sender.Send(new ForgotPasswordCommand(request.Email), ct);
        return result.Match(_ => Results.Ok(), _ => result.ToHttpResult());
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Échec → ValidationException (400) ou ErrorOr (ToHttpResult) selon la cause.
        var result = await sender.Send(
            new ResetPasswordCommand(request.UserId, request.Token, request.NewPassword), ct);
        return result.Match(_ => Results.Ok(), _ => result.ToHttpResult());
    }
}
