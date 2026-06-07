// The GET /products response has no named schema in the OpenAPI spec.
// Product shape is defined manually based on the known API contract {id, name, price}.
export interface Product {
  id: string;
  name: string;
  price: number;
}

// Re-export request DTOs from generated schema (@hey-api/openapi-ts → types.gen.ts)
export type { CreateProductCommand, LoginRequest, RegisterRequest } from './generated/types.gen';
