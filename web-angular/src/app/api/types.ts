import type { components } from './generated/schema';

// The GET /products response has no named schema in the OpenAPI spec.
// Product shape is defined manually based on the known API contract {id, name, price}.
export interface Product {
  id: string;
  name: string;
  price: number;
}

// Re-export request DTOs from generated schema
export type CreateProductCommand = components['schemas']['CreateProductCommand'];
export type LoginRequest = components['schemas']['LoginRequest'];
export type RegisterRequest = components['schemas']['RegisterRequest'];
