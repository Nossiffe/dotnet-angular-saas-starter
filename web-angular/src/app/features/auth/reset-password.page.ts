import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { form, FormField, required, minLength } from '@angular/forms/signals';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth/auth.service';
import { FormErrors } from '../../core/forms/form-errors';

@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './reset-password.page.html',
})
export class ResetPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  // Paramètres du lien de l'e-mail, liés par withComponentInputBinding
  readonly userId = input<string>('');
  readonly token = input<string>('');

  readonly model = signal({ newPassword: '' });
  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly resetForm = form(this.model, (p) => {
    required(p.newPassword, { message: 'Mot de passe requis' });
    minLength(p.newPassword, 12, { message: 'Au moins 12 caractères' });
  });
  /** Erreurs sous le champ modifié puis quitté, ou après un envoi refusé. */
  readonly errors = new FormErrors();

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.submitting() || !this.errors.check(this.resetForm)) return;
    const uid = this.userId();
    const tok = this.token();
    if (!uid || !tok) {
      this.serverError.set('Lien invalide ou expiré.');
      return;
    }
    this.submitting.set(true);
    this.serverError.set(null);
    this.auth.resetPassword(uid, tok, this.model().newPassword).subscribe({
      next: () => {
        this.submitting.set(false);
        this.snackBar.open('Mot de passe réinitialisé avec succès.', 'OK', { duration: 4000 });
        void this.router.navigateByUrl('/login');
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        if (err.status === 400) {
          this.serverError.set('Lien invalide ou expiré.');
        } else {
          this.serverError.set('Une erreur est survenue, veuillez réessayer.');
        }
      },
    });
  }
}
