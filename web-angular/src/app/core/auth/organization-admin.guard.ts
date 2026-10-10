import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Écran Organisation : Owner et Admin seulement, d'après le rôle du jeton. Un Member est renvoyé
 * vers les produits. L'API refuse de toute façon ses appels (403) : le garde évite seulement
 * d'afficher un écran inutilisable. À placer après `authGuard`.
 */
export const organizationAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.canManageOrganization() ? true : inject(Router).createUrlTree(['/products']);
};
