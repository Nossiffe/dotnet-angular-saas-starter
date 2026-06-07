import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
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

  add(): void {
    if (!this.name()) return;
    this.svc.create({ name: this.name(), price: this.price() }).subscribe(() => {
      this.name.set('');
      this.price.set(0);
      this.svc.products.reload();
    });
  }
  remove(id: string): void {
    this.svc.remove(id).subscribe(() => this.svc.products.reload());
  }
}
