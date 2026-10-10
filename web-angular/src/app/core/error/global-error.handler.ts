import { ErrorHandler, inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly snackBar = inject(MatSnackBar);

  handleError(error: unknown): void {
    // Les HttpErrorResponse sont déjà traitées par httpErrorInterceptor : pas de second toast.
    if (error instanceof HttpErrorResponse) {
      console.error(
        '[GlobalErrorHandler] HttpErrorResponse (already handled by interceptor)',
        error,
      );
      return;
    }
    console.error('[GlobalErrorHandler]', error);
    // Application zoneless : la snack bar n'a pas besoin de NgZone.run.
    this.snackBar.open('Une erreur inattendue est survenue.', 'Fermer', { duration: 6000 });
  }
}
