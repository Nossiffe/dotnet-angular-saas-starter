using ErrorOr;

namespace Saas.Domain.Tenancy;

public static class OrganizationErrors
{
    public static Error NotFound => Error.NotFound(
        code: "Organization.NotFound", description: "Organisation introuvable.");

    public static Error MemberNotFound => Error.NotFound(
        code: "Membership.NotFound", description: "Membre introuvable.");

    public static Error LastOwner => Error.Validation(
        code: "Organization.LastOwner",
        description: "Impossible de rétrograder le dernier Owner de l'organisation.");

    public static Error AlreadyMember => Error.Validation(
        code: "Membership.AlreadyMember",
        description: "Cette personne est déjà membre de l'organisation.");

    public static Error CannotChangeOwnRole => Error.Validation(
        code: "Membership.CannotChangeOwnRole",
        description: "Vous ne pouvez pas modifier votre propre rôle.");

    public static Error CannotRemoveOwner => Error.Validation(
        code: "Membership.CannotRemoveOwner",
        description: "Impossible de retirer un Owner de l'organisation.");

    public static Error InvalidRole => Error.Validation(
        code: "Membership.InvalidRole",
        description: "Rôle invalide. Valeurs acceptées : Member, Admin.");
}
