import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, httpResource } from '@angular/common/http';
import type { PagedResultOfProductDto, PostApiV1ProductsData } from '../../api/generated';
import { SKIP_ERROR_TOAST } from '../../core/error/http-error.interceptor';

// Corps de création = DTO généré `CreateProductCommand` (via le type Hey API). Pas d'interface à la main.
export type ProductInput = PostApiV1ProductsData['body'];

const skipToast = { context: new HttpContext().set(SKIP_ERROR_TOAST, true) };

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  readonly products = httpResource<PagedResultOfProductDto>(() => '/api/v1/products');

  create(body: ProductInput) {
    return this.http.post('/api/v1/products', body, skipToast);
  }
  remove(id: string) {
    return this.http.delete(`/api/v1/products/${id}`, skipToast);
  }
}
