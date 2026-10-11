import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { CurrencyPipe } from '@angular/common';
import { form, FormField, maxLength, pattern, required, validate } from '@angular/forms/signals';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import type { ProductDto } from '../../api/generated';
import { ProductsService } from './products.service';
import { FormErrors } from '../../core/forms/form-errors';
import { TableScroll } from '../../core/layout/table-scroll';

/** Longueur maximale du nom, comme la validation de l'API (UpdateProductValidator). */
export const NAME_MAX_LENGTH = 200;

/**
 * Un prix positif, avec deux décimales au plus, après un point ou une virgule. Au plus 13 chiffres
 * avant : au-delà, le nombre envoyé (un double JavaScript) perdrait des centimes, bien que la
 * colonne numeric(18,2) en accepte 16.
 */
const PRICE_PATTERN = /^\d{1,13}([.,]\d{1,2})?$/;

/**
 * Valeurs du formulaire, telles que tapées. Le prix reste un texte jusqu'à l'envoi : un champ
 * numérique réécrirait la saisie en cours (« 3. » devient « 3 », le curseur part au début).
 */
interface ProductFields {
  name: string;
  price: string;
}

const EMPTY_FIELDS: ProductFields = { name: '', price: '' };

function toPrice(text: string): number {
  return Number(text.replace(',', '.'));
}

/**
 * Produits de l'organisation : liste, ajout, modification et suppression. Un seul formulaire sert
 * à l'ajout et à la modification : « Modifier » le remplit avec le produit choisi. Les refus de
 * l'API s'affichent dans le toast global (httpErrorInterceptor).
 */
@Component({
  selector: 'app-products',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    FormField,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TableScroll,
  ],
  templateUrl: './products.page.html',
  styleUrl: './products.page.scss',
})
export class ProductsPage {
  protected readonly svc = inject(ProductsService);
  protected readonly cols = ['name', 'price', 'actions'];
  // Les règles de l'API : un nom ni vide ni fait d'espaces, de 200 caractères au plus, et un
  // prix positif ou nul. required et maxLength posent aussi les attributs natifs des champs.
  private readonly fields = signal<ProductFields>(EMPTY_FIELDS);
  protected readonly productForm = form(this.fields, (p) => {
    required(p.name, { message: 'Nom requis' });
    validate(p.name, ({ value }) =>
      value() !== '' && value().trim() === ''
        ? { kind: 'blank', message: 'Nom requis' }
        : undefined,
    );
    maxLength(p.name, NAME_MAX_LENGTH, { message: `Au plus ${NAME_MAX_LENGTH} caractères` });
    required(p.price, { message: 'Prix requis' });
    pattern(p.price, PRICE_PATTERN, {
      message: 'Un prix positif, avec deux décimales au plus (2,50)',
    });
  });
  protected readonly creating = signal(false);
  /** Produit en cours de modification dans le formulaire ; `null` : le formulaire ajoute. */
  protected readonly editing = signal<ProductDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly status = signal<string | null>(null);
  /** Un ajout ou une modification en cours : les actions des lignes attendent. */
  protected readonly busy = computed(() => this.creating() || this.saving());
  /**
   * Produit dont le bouton « Modifier » reçoit le focus une fois la liste relue, et l'élément qui
   * avait le focus à la fin de l'enregistrement.
   */
  private readonly focusAfterReload = signal<{ id: string; from: Element | null } | null>(null);

  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly nameInput = viewChild.required<ElementRef<HTMLInputElement>>('nameInput');

  constructor() {
    // Après un enregistrement, le focus revient au bouton « Modifier » du produit, une fois la
    // liste relue : renommé, le produit a pu changer de place, ou quitter la page (tri par nom,
    // 20 par page). S'il n'y est plus, le focus va au champ du nom. Si l'utilisateur a agi
    // pendant la relecture (une autre modification, une saisie) ou déplacé le focus, il reste.
    effect(() => {
      const pending = this.focusAfterReload();
      if (pending === null || this.svc.products.isLoading()) return;
      untracked(() => this.focusAfterReload.set(null));
      const active = document.activeElement;
      if (active !== null && active !== document.body && active !== pending.from) return;
      this.focus(() => this.editButton(pending.id) ?? this.nameInput().nativeElement);
    });
  }

  /**
   * Les erreurs s'affichent sur un champ modifié puis quitté, ou sur tout le formulaire après un
   * envoi refusé (FormErrors). Après Échap ou un enregistrement, la page déplace le focus : le champ
   * vidé est quitté, mais pas modifié, et reste sans erreur.
   */
  protected readonly errors = new FormErrors();

  // Une saisie de l'utilisateur ; le formulaire, lui, reçoit la valeur par [formField].
  userInput(): void {
    this.focusAfterReload.set(null);
  }

  submit(): void {
    if (this.editing()) {
      this.save();
    } else {
      this.add();
    }
  }

  add(): void {
    if (this.creating() || !this.checkValid()) return;
    const { name, price } = this.fields();
    this.creating.set(true);
    this.status.set(null);
    this.svc
      .create({ name, price: toPrice(price) })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.creating.set(false)),
      )
      // Le bouton « Ajouter », désactivé pendant l'envoi, a perdu le focus : il va au nom, prêt
      // pour le produit suivant ou pour corriger celui-ci. Le toast global affiche un refus.
      .subscribe({
        next: () => {
          this.resetForm();
          this.svc.products.reload();
          this.focus(() => this.nameInput().nativeElement);
        },
        error: () => this.focus(() => this.nameInput().nativeElement),
      });
  }

  // Le formulaire passe en modification, rempli avec le produit ; le focus va au nom.
  edit(product: ProductDto): void {
    this.focusAfterReload.set(null);
    this.editing.set(product);
    this.fill({ name: product.name, price: Number(product.price).toFixed(2) });
    this.status.set(null);
    this.focus(() => this.nameInput().nativeElement);
  }

  // Retour au formulaire d'ajout, sans rien envoyer ; le focus revient au bouton « Modifier ».
  cancel(): void {
    const product = this.editing();
    if (!product || this.saving()) return;
    this.resetForm();
    this.focus(() => this.editButton(product.id));
  }

  remove(id: string): void {
    this.status.set(null);
    this.svc
      .remove(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.editing()?.id === id) this.resetForm();
        this.svc.products.reload();
      });
  }

  private save(): void {
    const product = this.editing();
    if (!product || this.saving() || !this.checkValid()) return;
    const { name, price } = this.fields();
    this.saving.set(true);
    this.svc
      .update(product.id, { name, price: toPrice(price) })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.resetForm();
          this.status.set(`« ${name} » est enregistré.`);
          this.svc.products.reload();
          this.focusAfterReload.set({ id: product.id, from: document.activeElement });
        },
        // Le toast global affiche le refus. Produit supprimé entre-temps (404) : le formulaire
        // revient à l'ajout ; sinon il reste en modification, pour corriger ou annuler. Dans les
        // deux cas, le focus va au nom (« Enregistrer », désactivé pendant l'envoi, l'a perdu).
        error: (err: unknown) => {
          if (err instanceof HttpErrorResponse && err.status === 404) this.resetForm();
          this.svc.products.reload();
          this.focus(() => this.nameInput().nativeElement);
        },
      });
  }

  // Formulaire invalide : rien ne part, et les champs affichent leur erreur, même sans avoir été
  // modifiés ni quittés (Entrée dans un champ, formulaire vide). Le formulaire a novalidate : la
  // page affiche ses propres messages au lieu de la bulle du navigateur.
  private checkValid(): boolean {
    return this.errors.check(this.productForm);
  }

  private resetForm(): void {
    this.editing.set(null);
    this.fill(EMPTY_FIELDS);
  }

  // Toute valeur posée par le code passe par reset(), qui écrit aussi les champs du DOM, tout de
  // suite. Un simple set() ne les écrirait qu'au rendu suivant, et seulement si la valeur a changé
  // depuis la dernière écriture : un texte tapé depuis resterait affiché, par exemple après
  // « Modifier », une saisie et Échap sans rendu entre les trois.
  private fill(values: ProductFields): void {
    this.errors.clear();
    this.productForm().reset({ ...values });
  }

  private editButton(id: string): HTMLElement | null {
    return this.host.nativeElement.querySelector<HTMLElement>(`[data-edit-id="${id}"]`);
  }

  private focus(target: () => HTMLElement | null): void {
    afterNextRender(() => target()?.focus(), { injector: this.injector });
  }
}
