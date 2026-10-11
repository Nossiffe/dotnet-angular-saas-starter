import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { form, FormField, required, minLength } from '@angular/forms/signals';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { InvitationsService } from './invitations.service';
import { FormErrors } from '../../core/forms/form-errors';

// Code stable de l'extension `code` du ProblemDetails (docs/error-codes.md) : l'invitation est
// valide, mais aucun compte n'existe pour cet e-mail.
const PASSWORD_REQUIRED = 'Invitation.PasswordRequired';

interface Problem {
  code?: string;
  errors?: Record<string, string[]>;
}

function problemOf(err: HttpErrorResponse): Problem | null {
  return typeof err.error === 'object' && err.error !== null ? (err.error as Problem) : null;
}

// Identifiant d'invitation au format GUID : un lien tronqué ou modifié est refusé sans appel à
// l'API.
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Erreurs de saisie (FluentValidation ou refus d'Identity) : un dictionnaire `errors` par champ, sans
// `code`. Une erreur métier porte un `code`, et liste aussi ses erreurs dans `errors`.
function isInputValidation(problem: Problem | null): boolean {
  return problem?.errors !== undefined && problem.code === undefined;
}

// Erreurs de saisie portant sur le mot de passe : la clé `Password` (FluentValidation) ou un code
// d'Identity (`PasswordTooShort`, `PasswordRequiresDigit`…).
function refusesPassword(problem: Problem | null): boolean {
  return Object.keys(problem?.errors ?? {}).some((key) => key.startsWith('Password'));
}

type Step = 'checking' | 'password' | 'accepted' | 'invalid' | 'error';

@Component({
  selector: 'app-accept-invitation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './accept-invitation.page.html',
})
export class AcceptInvitationPage implements OnInit {
  private readonly invitations = inject(InvitationsService);

  // Paramètres du lien de l'e-mail d'invitation, liés par withComponentInputBinding
  readonly invitationId = input<string>('');
  readonly token = input<string>('');

  readonly step = signal<Step>('checking');
  readonly isNewUser = signal(false);
  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly model = signal({ password: '' });
  readonly passwordForm = form(this.model, (p) => {
    required(p.password, { message: 'Mot de passe requis' });
    minLength(p.password, 12, { message: 'Au moins 12 caractères' });
  });
  /** Erreurs sous le champ modifié puis quitté, ou après un envoi refusé. */
  readonly errors = new FormErrors();

  ngOnInit(): void {
    if (!GUID.test(this.invitationId()) || !this.token()) {
      this.step.set('invalid');
      return;
    }
    // Premier appel sans mot de passe : il suffit si le compte existe déjà.
    this.accept(null);
  }

  retry(): void {
    this.step.set('checking');
    this.accept(null);
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    if (this.submitting() || !this.errors.check(this.passwordForm)) return;
    this.accept(this.model().password);
  }

  private accept(password: string | null): void {
    this.submitting.set(true);
    this.serverError.set(null);
    this.invitations
      .accept({ invitationId: this.invitationId(), token: this.token(), password })
      .subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.isNewUser.set(res.isNewUser);
          this.step.set('accepted');
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.onError(err, password !== null);
        },
      });
  }

  private onError(err: HttpErrorResponse, withPassword: boolean): void {
    const problem = problemOf(err);
    if (err.status === 400 && problem?.code === PASSWORD_REQUIRED) {
      this.step.set('password');
    } else if (withPassword && err.status === 400 && isInputValidation(problem)) {
      // Refus des règles de mot de passe d'Identity : l'API les décrit en anglais, sous des codes
      // propres à Identity. La page rappelle les règles affichées au-dessus du champ. Une autre
      // erreur de validation (e-mail pris entre les deux appels, par exemple) reste générique.
      this.serverError.set(
        refusesPassword(problem)
          ? 'Ce mot de passe ne respecte pas ces règles.'
          : 'Une erreur est survenue, veuillez réessayer.',
      );
    } else if (err.status === 400 || err.status === 404) {
      // Invitation inconnue, jeton faux, expirée, déjà acceptée ou révoquée : un seul message.
      this.step.set('invalid');
    } else if (withPassword) {
      this.serverError.set('Une erreur est survenue, veuillez réessayer.');
    } else {
      this.step.set('error');
    }
  }
}
