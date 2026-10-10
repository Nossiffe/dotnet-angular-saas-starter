import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { problemMessage } from './problem-message';

const error = (status: number, body: unknown) =>
  new HttpErrorResponse({ status, error: body, statusText: 'Error' });

describe('problemMessage', () => {
  // Un message par code du registre (docs/error-codes.md) que l'écran peut recevoir.
  it.each([
    ['Organization.NotFound', "Cette organisation n'est plus accessible avec votre session."],
    ['Organization.LastOwner', "Impossible de rétrograder le dernier Owner de l'organisation."],
    ['Membership.NotFound', "Ce membre ne fait plus partie de l'organisation."],
    ['Membership.AlreadyMember', "Cette personne est déjà membre de l'organisation."],
    ['Membership.CannotChangeOwnRole', 'Vous ne pouvez pas modifier votre propre rôle.'],
    ['Membership.CannotRemoveOwner', "Un Owner ne peut pas être retiré de l'organisation."],
    ['Membership.InvalidRole', 'Ce rôle ne peut pas être attribué.'],
    ['Invitation.NotFound', "Cette invitation n'existe plus."],
    ['Invitation.NotPending', "Cette invitation n'est plus en attente."],
    ['Invitation.AlreadyPending', 'Une invitation est déjà en attente pour cette adresse.'],
    ['Invitation.OwnerRole', 'Ce rôle ne peut pas être attribué par invitation.'],
    ['Auth.SessionExpired', 'Votre session a expiré. Reconnectez-vous.'],
  ])('maps %s', (code, message) => {
    expect(problemMessage(error(400, { code, errors: { [code]: ['…'] } }))).toBe(message);
  });

  it.each([
    [400, { errors: { Email: ['invalid'] } }, 'Adresse e-mail invalide.'],
    [400, { errors: { Role: ['invalid'] } }, 'La demande a été refusée : vérifiez la saisie.'],
    [401, null, 'Votre session a expiré. Reconnectez-vous.'],
    [403, null, 'Votre rôle ne permet pas cette action.'],
    [409, { title: 'conflit' }, 'Cette ressource a été modifiée ailleurs, réessayez.'],
    [500, null, 'Une erreur est survenue, réessayez.'],
    [0, null, 'Une erreur est survenue, réessayez.'],
  ])('falls back on the status %s without a known code', (status, body, message) => {
    expect(problemMessage(error(status, body))).toBe(message);
  });

  it('handles an error that is not an HTTP response', () => {
    expect(problemMessage(new Error('boom'))).toBe('Une erreur est survenue, réessayez.');
  });
});
