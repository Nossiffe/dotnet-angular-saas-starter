import { ErrorHandler, inject, Injectable, NgZone } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  // MatSnackBar needs NgZone to show in the Angular zone when called from outside
  private readonly zone = inject(NgZone);
  private readonly snackBar = inject(MatSnackBar);

  handleError(error: unknown): void {
    console.error('[GlobalErrorHandler]', error);
    this.zone.run(() => {
      this.snackBar.open('Une erreur inattendue est survenue.', 'Fermer', { duration: 6000 });
    });
  }
}
