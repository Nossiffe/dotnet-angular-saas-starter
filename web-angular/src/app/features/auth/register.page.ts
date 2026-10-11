import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { form, FormField, required, email, minLength } from '@angular/forms/signals';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';
import { FormErrors } from '../../core/forms/form-errors';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './register.page.html',
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly model = signal({ email: '', password: '' });
  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly registerForm = form(this.model, (p) => {
    required(p.email, { message: 'Email requis' });
    email(p.email, { message: 'Email invalide' });
    required(p.password, { message: 'Mot de passe requis' });
    minLength(p.password, 12, { message: 'Au moins 12 caractères' });
  });
  /** Erreurs sous un champ modifié puis quitté, ou sous tous après un envoi refusé. */
  readonly errors = new FormErrors();

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.submitting() || !this.errors.check(this.registerForm)) return;
    this.submitting.set(true);
    this.serverError.set(null);
    this.auth.register(this.model()).subscribe({
      next: () => {
        this.submitting.set(false);
        void this.router.navigateByUrl('/login');
      },
      error: () => {
        this.serverError.set("Échec de l'inscription");
        this.submitting.set(false);
      },
    });
  }
}
