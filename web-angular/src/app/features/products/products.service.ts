import { Injectable, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import type { Product, ProductsPage } from '../../api/types';

export type ProductInput = Pick<Product, 'name' | 'price'>;

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  readonly products = httpResource<ProductsPage>(() => '/api/v1/products');

  create(body: ProductInput) {
    return this.http.post('/api/v1/products', body);
  }
  remove(id: string) {
    return this.http.delete(`/api/v1/products/${id}`);
  }
}
