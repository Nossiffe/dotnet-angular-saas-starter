import type { Page } from '@playwright/test';
import { mockOrganization, type Call } from './organization-mocks';

export const PEN_ID = '0199c9a0-0000-7000-8000-000000000011';
export const INK_ID = '0199c9a0-0000-7000-8000-000000000012';

interface Product {
  id: string;
  name: string;
  price: number;
}

/**
 * A signed-in Owner (see mockOrganization) and the products API as an in-memory list: the list,
 * the change (PUT) and the removal read and change it like the real API. Returns the calls made.
 */
export async function mockProducts(page: Page): Promise<Call[]> {
  await mockOrganization(page, 'Owner');
  const calls: Call[] = [];
  const products: Product[] = [
    { id: PEN_ID, name: 'Stylo', price: 2.5 },
    { id: INK_ID, name: 'Encre', price: 4 },
  ];

  // Registered after mockOrganization, this route answers first for the products.
  await page.route('**/api/v1/products**', async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace('/api/v1', '');
    const method = request.method();
    calls.push({ method, path, body: request.postDataJSON() ?? undefined });
    const id = path.split('/')[2];

    if (method === 'GET' && !id) {
      return route.fulfill({
        json: {
          items: products,
          page: 1,
          pageSize: 20,
          totalCount: products.length,
          totalPages: 1,
        },
      });
    }
    const product = products.find((p) => p.id === id);
    if (!product) return route.fulfill({ status: 404 });
    if (method === 'PUT') {
      Object.assign(product, request.postDataJSON() as Omit<Product, 'id'>);
      return route.fulfill({ status: 204 });
    }
    if (method === 'DELETE') {
      products.splice(products.indexOf(product), 1);
      return route.fulfill({ status: 204 });
    }
    return route.fulfill({ status: 404 });
  });
  return calls;
}
