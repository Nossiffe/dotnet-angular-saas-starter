// Re-export all generated types
export type {
  ProductDto,
  PagedResultOfProductDto,
  CreateProductCommand,
  LoginCommand,
  RegisterCommand,
  AuthResponse,
  RegisterResponse,
} from './generated/types.gen';

// Alias for backward-compat with components using the `Product` name
export type { ProductDto as Product } from './generated/types.gen';
export type { PagedResultOfProductDto as ProductsPage } from './generated/types.gen';
