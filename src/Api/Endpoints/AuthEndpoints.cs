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
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Logout : AllowAnonymous délibéré. La credential réelle du logout EST le cookie HttpOnly
        // `rt` (possession), pas l'access token — la révocation est idempotente et la suppression du
        // cookie est inoffensive ; un access token expiré/absent ne doit donc jamais empêcher un
        // utilisateur de se déconnecter. SameSite=Strict couvre déjà le logout-CSRF (nuisance
        // "je te déconnecte" seulement, aucune lecture ni écriture de données possible).
        group.MapPost("/logout", Logout)
            .AllowAnonymous()
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

    // 201 sans Location : l'API n'expose pas de ressource « utilisateur » à lire.
    private static async Task<IResult> Register(RegisterRequest request, ISender sender, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.Send(new RegisterCommand(request.Email, request.Password), ct);
        return result.Match(
            id => Results.Created((string?)null, new RegisterResponse(id)),
            _ => result.ToHttpResult());
    }

    private static async Task<IResult> Login(LoginRequest request, ISender sender, HttpResponse response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await sender.Send(new LoginCommand(request.Email, request.Password, request.TenantId), ct);
        return result.Match(
            auth =>
            {
                RefreshCookie.Append(response, auth.RefreshToken, auth.RefreshTokenDays);
                return TypedResults.Ok(new AuthResponse(auth.AccessToken, auth.ExpiresAt)) as IResult;
            },
            _ => result.ToHttpResult());
    }

    private static async Task<IResult> Refresh(ISender sender, HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var raw = RefreshCookie.Read(request);
        ErrorOr.ErrorOr<Saas.Application.Auth.AuthResult> result = raw is null
            ? Saas.Application.Auth.AuthErrors.SessionExpired
            : await sender.Send(new RefreshTokenCommand(raw), ct);
        return result.Match(
            auth =>
            {
                RefreshCookie.Append(response, auth.RefreshToken, auth.RefreshTokenDays);
                return TypedResults.Ok(new AuthResponse(auth.AccessToken, auth.ExpiresAt)) as IResult;
            },
            _ =>
            {
                // Le token refusé ne doit pas rester dans le navigateur : présenté de nouveau, un token
                // révoqué déclenche la détection de réutilisation, qui ferme toutes les sessions.
                RefreshCookie.Delete(response);
                return result.ToHttpResult();
            });
    }

    private static async Task<IResult> Logout(ISender sender, HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var raw = RefreshCookie.Read(request);
        if (raw is not null)
        {
            _ = await sender.Send(new LogoutCommand(raw), ct); // révocation idempotente
        }

        RefreshCookie.Delete(response);
        return Results.NoContent();
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
        var result = await sender.Send(
            new ResetPasswordCommand(request.UserId, request.Token, request.NewPassword), ct);
        return result.Match(_ => Results.Ok(), _ => result.ToHttpResult());
    }
}
