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
  protected readonly price = signal(0);
  protected readonly creating = signal(false);

  private readonly destroyRef = inject(DestroyRef);

  add(): void {
    if (!this.name() || this.price() <= 0 || this.creating()) return;
    this.creating.set(true);
    this.svc
      .create({ name: this.name(), price: this.price() })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.creating.set(false)),
      )
      .subscribe(() => {
        this.name.set('');
        this.price.set(0);
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
