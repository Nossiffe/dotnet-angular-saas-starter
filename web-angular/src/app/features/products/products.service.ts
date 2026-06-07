import { Injectable, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import type { Product } from '../../api/types';

export type ProductInput = Pick<Product, 'name' | 'price'>;

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  readonly products = httpResource<Product[]>(() => '/products');

  create(body: ProductInput) { return this.http.post('/products', body); }
  remove(id: string) { return this.http.delete(`/products/${id}`); }
}
