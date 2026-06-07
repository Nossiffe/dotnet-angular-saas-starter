import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { form, FormField, required, email, minLength } from '@angular/forms/signals';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';

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
    required(p.email);
    email(p.email, { message: 'Email invalide' });
    required(p.password);
    minLength(p.password, 12, { message: 'Au moins 12 caractères' });
  });

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.registerForm().invalid()) return;
    this.submitting.set(true);
    this.serverError.set(null);
    this.auth.register(this.model()).subscribe({
      next: () => void this.router.navigateByUrl('/login'),
      error: () => {
        this.serverError.set("Échec de l'inscription");
        this.submitting.set(false);
      },
    });
  }
}
