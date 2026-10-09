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

    public static Error InvalidResetLink => Error.Validation(
        code: "Auth.InvalidResetLink",
        description: "Lien de réinitialisation invalide ou expiré.");

    // Ne s'obtient qu'avec le bon mot de passe : le message ne révèle rien à qui ne l'a pas.
    public static Error EmailNotConfirmed => Error.Validation(
        code: "Auth.EmailNotConfirmed",
        description: "Veuillez confirmer votre adresse e-mail avant de vous connecter.");
}
