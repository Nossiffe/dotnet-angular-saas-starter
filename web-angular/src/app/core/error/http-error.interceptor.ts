import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { throwError, catchError } from 'rxjs';

interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

/**
 * Parses an HttpErrorResponse body as ProblemDetails and returns a user-friendly message.
 * Returns null if the error should NOT be toasted (e.g. 401 handled by authInterceptor).
 */
function extractMessage(err: HttpErrorResponse): string | null {
  // 401 is handled by authInterceptor (token refresh) — do not toast
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
      if (err instanceof HttpErrorResponse) {
        const msg = extractMessage(err);
        if (msg) {
          snackBar.open(msg, 'Fermer', { duration: 6000 });
        }
      }
      return throwError(() => err);
    }),
  );
};
