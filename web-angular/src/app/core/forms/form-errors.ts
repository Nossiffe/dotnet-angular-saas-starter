import { signal } from '@angular/core';
import type { ErrorStateMatcher } from '@angular/material/core';

/** Ce que la règle lit d'un champ de formulaire signal (FieldTree) : son état et ses erreurs. */
type Field = () => {
  invalid(): boolean;
  dirty(): boolean;
  touched(): boolean;
  errors(): readonly { message?: string }[];
};

/**
 * Quand un formulaire affiche ses erreurs, la même règle sur toutes les pages : sous un champ que
 * l'utilisateur a modifié puis quitté, ou sous tous les champs après un envoi refusé. Pas sous un
 * champ seulement « touché » : le focus déplacé par la page, après une remise à zéro par exemple,
 * marquerait le champ vidé comme touché.
 *
 * Dans le gabarit, chaque champ prend `[errorStateMatcher]="errors.matcher"`, et son message un seul
 * élément racine dans le `@if`, que mat-form-field projette sous le champ :
 * `@if (errors.shown(f.email)) { <mat-error>{{ errors.text(f.email) }}</mat-error> }`.
 * Le formulaire a `novalidate` : la page affiche ses messages, pas la bulle du navigateur.
 */
export class FormErrors {
  private readonly refused = signal(false);

  /**
   * La même règle pour l'état d'erreur de Material (contour rouge, aria-invalid). Les champs sont
   * liés par [formField] : Material appelle isSignalErrorState ; isErrorState sert aux formulaires
   * réactifs.
   */
  readonly matcher: ErrorStateMatcher = {
    isErrorState: (control) =>
      !!control?.invalid && ((control.dirty && control.touched) || this.refused()),
    isSignalErrorState: (field) => !!field && this.shownFor(field()),
  };

  /** Le champ affiche-t-il ses erreurs ? */
  shown(field: Field): boolean {
    return this.shownFor(field());
  }

  /** Les messages d'erreur du champ, à la suite, pour son mat-error. */
  text(field: Field): string {
    return field()
      .errors()
      .map((e) => e.message)
      .join(' ');
  }

  /**
   * À l'envoi : vrai si le formulaire est valide. Sinon, l'envoi est refusé, et tous les champs
   * affichent leurs erreurs, même sans avoir été modifiés ni quittés.
   */
  check(form: () => { valid(): boolean }): boolean {
    if (form().valid()) return true;
    this.refused.set(true);
    return false;
  }

  /** Après une remise à zéro du formulaire : plus d'erreur jusqu'à la prochaine saisie ou le prochain envoi. */
  clear(): void {
    this.refused.set(false);
  }

  private shownFor(state: { invalid(): boolean; dirty(): boolean; touched(): boolean }): boolean {
    return state.invalid() && ((state.dirty() && state.touched()) || this.refused());
  }
}
