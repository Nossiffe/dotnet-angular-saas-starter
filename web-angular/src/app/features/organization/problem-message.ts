import { HttpErrorResponse } from '@angular/common/http';

interface Problem {
  code?: string;
  errors?: Record<string, string[]>;
}

// Messages par code stable de l'extension `code` du ProblemDetails (docs/error-codes.md). Le texte
// vient du front : le code, lui, ne change pas.
const BY_CODE: Record<string, string> = {
  'Organization.NotFound': "Cette organisation n'est plus accessible avec votre session.",
  'Organization.LastOwner': "Impossible de rétrograder le dernier Owner de l'organisation.",
  'Membership.NotFound': "Ce membre ne fait plus partie de l'organisation.",
  'Membership.AlreadyMember': "Cette personne est déjà membre de l'organisation.",
  'Membership.CannotChangeOwnRole': 'Vous ne pouvez pas modifier votre propre rôle.',
  'Membership.CannotRemoveOwner': "Un Owner ne peut pas être retiré de l'organisation.",
  'Membership.InvalidRole': 'Ce rôle ne peut pas être attribué.',
  'Invitation.NotFound': "Cette invitation n'existe plus.",
  'Invitation.NotPending': "Cette invitation n'est plus en attente.",
  'Invitation.AlreadyPending': 'Une invitation est déjà en attente pour cette adresse.',
  'Invitation.OwnerRole': 'Ce rôle ne peut pas être attribué par invitation.',
  'Auth.SessionExpired': 'Votre session a expiré. Reconnectez-vous.',
};

function problemOf(err: HttpErrorResponse): Problem | null {
  return typeof err.error === 'object' && err.error !== null ? (err.error as Problem) : null;
}

/**
 * Message affiché pour une erreur de l'API, selon son code. Un 401 arrive ici seulement si le
 * refresh a échoué (authInterceptor a déjà tenté de renouveler la session) : la session est
 * terminée. Sans code connu, un message selon le statut ; `conflict` remplace celui d'un 409 sans
 * code, quand l'action donne à ce conflit un sens précis.
 */
export function problemMessage(err: unknown, conflict?: string): string {
  if (!(err instanceof HttpErrorResponse)) return 'Une erreur est survenue, réessayez.';
  const problem = problemOf(err);
  const known = problem?.code ? BY_CODE[problem.code] : undefined;
  if (known) return known;
  switch (err.status) {
    case 400:
      // Erreur de saisie (FluentValidation) : un dictionnaire `errors` par champ, sans `code`.
      return problem?.errors && 'Email' in problem.errors
        ? 'Adresse e-mail invalide.'
        : 'La demande a été refusée : vérifiez la saisie.';
    case 401:
      return 'Votre session a expiré. Reconnectez-vous.';
    case 403:
      return 'Votre rôle ne permet pas cette action.';
    case 409:
      return conflict ?? 'Cette ressource a été modifiée ailleurs, réessayez.';
    default:
      return 'Une erreur est survenue, réessayez.';
  }
}
