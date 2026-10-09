using ErrorOr;

namespace Saas.Domain.Tenancy;

public static class InvitationErrors
{
    public static Error NotFound => Error.NotFound(
        code: "Invitation.NotFound", description: "Invitation introuvable.");

    public static Error OwnerRole => Error.Validation(
        code: "Invitation.OwnerRole",
        description: "Impossible d'inviter un membre avec le rôle Owner.");

    public static Error AlreadyPending => Error.Conflict(
        code: "Invitation.AlreadyPending",
        description: "Une invitation est déjà en attente pour cet email.");

    public static Error NotPendingRevoke => Error.Validation(
        code: "Invitation.NotPending",
        description: "Seule une invitation en attente peut être révoquée.");

    public static Error NotPendingAccept => Error.Validation(
        code: "Invitation.NotPending",
        description: "Seule une invitation en attente peut être acceptée.");

    public static Error NotPendingRefresh => Error.Validation(
        code: "Invitation.NotPending",
        description: "Seule une invitation en attente peut être renouvelée.");

    public static Error NotPendingIssueToken => Error.Validation(
        code: "Invitation.NotPending",
        description: "Seule une invitation en attente peut recevoir un nouveau lien.");

    public static Error Expired => Error.Validation(
        code: "Invitation.Expired", description: "Cette invitation a expiré.");

    // Message anti-énumération : ne révèle pas si l'invitation existe, est expirée, révoquée ou si
    // le token est faux (AcceptInvitationHandler, avant vérification du token).
    public static Error Invalid => Error.Validation(
        code: "Invitation.Invalid", description: "Invitation invalide ou expirée.");

    public static Error PasswordRequired => Error.Validation(
        code: "Invitation.PasswordRequired",
        description: "Un mot de passe est requis pour finaliser la création du compte.");
}
