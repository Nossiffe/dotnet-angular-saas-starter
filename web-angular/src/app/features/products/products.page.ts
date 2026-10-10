import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { finalize } from 'rxjs';
import { ProductsService } from './products.service';

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

  private readonly destroyRef = inject(DestroyRef);

  // Champ vide : aucun prix, et non un prix de 0.
  setPrice(value: string): void {
    this.price.set(value === '' ? null : Number(value));
  }

  add(): void {
    const price = this.price();
    if (!this.name() || price === null || !(price >= 0) || this.creating()) return;
    this.creating.set(true);
    this.svc
      .create({ name: this.name(), price })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.creating.set(false)),
      )
      .subscribe(() => {
        this.name.set('');
        this.price.set(null);
        this.svc.products.reload();
      });
  }
  remove(id: string): void {
    this.svc
      .remove(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.svc.products.reload());
  }
}
