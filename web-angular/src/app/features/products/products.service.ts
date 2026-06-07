import { Injectable, inject } from '@angular/core';
import { HttpClient, httpResource } from '@angular/common/http';
import type { PagedResultOfProductDto, PostApiV1ProductsData } from '../../api/generated';

// Corps de création = DTO généré `CreateProductCommand` (via le type Hey API). Pas d'interface à la main.
export type ProductInput = PostApiV1ProductsData['body'];

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  readonly products = httpResource<PagedResultOfProductDto>(() => '/api/v1/products');

  create(body: ProductInput) {
    return this.http.post('/api/v1/products', body);
  }
  remove(id: string) {
    return this.http.delete(`/api/v1/products/${id}`);
  }
}
