import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { throwError, catchError } from 'rxjs';

/**
 * À `true` dans le contexte d'une requête, httpErrorInterceptor n'affiche pas de snack bar pour ses
 * erreurs. Pour les appels dont la page affiche elle-même l'erreur (inscription, mot de passe
 * oublié, réinitialisation, acceptation d'une invitation…).
 */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/**
 * Lit le corps d'une HttpErrorResponse comme un ProblemDetails et en tire un message lisible.
 * Renvoie null quand l'erreur ne doit pas être affichée (un 401, traité par authInterceptor).
 */
function extractMessage(err: HttpErrorResponse): string | null {
  // 401 : authInterceptor renouvelle le jeton, rien à afficher
  if (err.status === 401) return null;

  if (err.status === 409) {
    return 'Cette ressource a été modifiée ailleurs, réessayez.';
  }

  const body = err.error as ProblemDetails | null;
  if (body && typeof body === 'object') {
    const detail = body.detail ?? body.title;
    if (detail) return detail;
  }

  return `Erreur ${err.status} : ${err.statusText || 'Erreur serveur'}`;
}

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && !req.context.get(SKIP_ERROR_TOAST)) {
        const msg = extractMessage(err);
        if (msg) {
          snackBar.open(msg, 'Fermer', { duration: 6000 });
        }
      }
      return throwError(() => err);
    }),
  );
};
