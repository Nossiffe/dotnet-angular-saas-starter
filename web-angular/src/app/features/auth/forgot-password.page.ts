import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { form, FormField, required, email } from '@angular/forms/signals';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';
import { FormErrors } from '../../core/forms/form-errors';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './forgot-password.page.html',
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);

  readonly model = signal({ email: '' });
  readonly submitting = signal(false);
  readonly sent = signal(false);

  readonly forgotForm = form(this.model, (p) => {
    required(p.email, { message: 'Email requis' });
    email(p.email, { message: 'Email invalide' });
  });
  /** Erreurs sous le champ modifié puis quitté, ou après un envoi refusé. */
  readonly errors = new FormErrors();

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.submitting() || !this.errors.check(this.forgotForm)) return;
    this.submitting.set(true);
    this.auth.forgotPassword(this.model().email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.sent.set(true);
      },
      error: () => {
        // Anti-énumération : toujours le même message neutre, même en cas d'erreur
        this.submitting.set(false);
        this.sent.set(true);
      },
    });
  }
}
