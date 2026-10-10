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
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import type { ProductDto } from '../../api/generated';
import { ProductsService } from './products.service';

/** Longueur maximale du nom, comme la validation de l'API (UpdateProductValidator). */
export const NAME_MAX_LENGTH = 200;

/**
 * Produits de l'organisation : liste, ajout, modification et suppression. Un seul formulaire sert
 * à l'ajout et à la modification : « Modifier » le remplit avec le produit choisi. Les refus de
 * l'API s'affichent dans le toast global (httpErrorInterceptor).
 */
@Component({
  selector: 'app-products',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CurrencyPipe, MatTableModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './products.page.html',
})
export class ProductsPage {
  protected readonly svc = inject(ProductsService);
  protected readonly cols = ['name', 'price', 'actions'];
  protected readonly name = signal('');
  protected readonly price = signal<number | null>(null);
  protected readonly creating = signal(false);
  /** Produit en cours de modification dans le formulaire ; `null` : le formulaire ajoute. */
  protected readonly editing = signal<ProductDto | null>(null);
  protected readonly saving = signal(false);
  protected readonly status = signal<string | null>(null);
  protected readonly nameMaxLength = NAME_MAX_LENGTH;
  /** Un ajout ou une modification en cours : les actions des lignes attendent. */
  protected readonly busy = computed(() => this.creating() || this.saving());

  // Les règles de l'API : un nom ni vide ni fait d'espaces, de 200 caractères au plus, et un
  // prix positif ou nul.
  private readonly valid = computed(() => {
    const name = this.name();
    const price = this.price();
    return name.trim() !== '' && name.length <= NAME_MAX_LENGTH && price !== null && price >= 0;
  });
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

  setName(value: string): void {
    this.focusAfterReload.set(null);
    this.name.set(value);
  }

  // Champ vide : aucun prix, et non un prix de 0.
  setPrice(value: string): void {
    this.focusAfterReload.set(null);
    this.price.set(value === '' ? null : Number(value));
  }

  submit(): void {
    if (this.editing()) {
      this.save();
    } else {
      this.add();
    }
  }

  add(): void {
    const price = this.price();
    if (!this.valid() || price === null || this.creating()) return;
    this.creating.set(true);
    this.status.set(null);
    this.svc
      .create({ name: this.name(), price })
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
    this.name.set(product.name);
    this.price.set(Number(product.price));
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
    const price = this.price();
    if (!product || !this.valid() || price === null || this.saving()) return;
    const name = this.name();
    this.saving.set(true);
    this.svc
      .update(product.id, { name, price })
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

  private resetForm(): void {
    this.editing.set(null);
    this.name.set('');
    this.price.set(null);
  }

  private editButton(id: string): HTMLElement | null {
    return this.host.nativeElement.querySelector<HTMLElement>(`[data-edit-id="${id}"]`);
  }

  private focus(target: () => HTMLElement | null): void {
    afterNextRender(() => target()?.focus(), { injector: this.injector });
  }
}
