import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { form, FormField, required, email, minLength } from '@angular/forms/signals';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormField, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './login.page.html',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly model = signal({ email: '', password: '' });
  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly loginForm = form(this.model, (p) => {
    required(p.email);
    email(p.email, { message: 'Email invalide' });
    required(p.password);
    minLength(p.password, 8, { message: 'Au moins 8 caractères' });
  });

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.loginForm().invalid()) return;
    this.submitting.set(true);
    this.serverError.set(null);
    this.auth.login(this.model()).subscribe({
      next: () => void this.router.navigateByUrl('/products'),
      error: () => { this.serverError.set('Identifiants invalides'); this.submitting.set(false); },
    });
  }
}
