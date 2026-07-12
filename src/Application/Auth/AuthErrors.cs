using ErrorOr;

namespace Saas.Application.Auth;

public static class AuthErrors
{
    public static Error InvalidCredentials => Error.Unauthorized(
        code: "Auth.InvalidCredentials", description: "Identifiants invalides.");

    public static Error SessionExpired => Error.Unauthorized(
        code: "Auth.SessionExpired", description: "Session expirée, reconnectez-vous.");

    public static Error InvalidConfirmationLink => Error.Validation(
        code: "Auth.InvalidConfirmationLink",
        description: "Lien de confirmation invalide ou expiré.");
}
