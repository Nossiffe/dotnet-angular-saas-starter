import { ErrorHandler, inject, Injectable, NgZone } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  // MatSnackBar needs NgZone to show in the Angular zone when called from outside
  private readonly zone = inject(NgZone);
  private readonly snackBar = inject(MatSnackBar);

  handleError(error: unknown): void {
    // HttpErrorResponse errors are already handled by httpErrorInterceptor — skip to avoid
    // double-toasting.
    if (error instanceof HttpErrorResponse) {
      console.error(
        '[GlobalErrorHandler] HttpErrorResponse (already handled by interceptor)',
        error,
      );
      return;
    }
    console.error('[GlobalErrorHandler]', error);
    this.zone.run(() => {
      this.snackBar.open('Une erreur inattendue est survenue.', 'Fermer', { duration: 6000 });
    });
  }
}
