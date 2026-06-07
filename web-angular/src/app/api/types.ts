// Re-export all generated types
export type {
  ProductDto,
  CreateProductCommand,
  LoginCommand,
  RegisterCommand,
  AuthResponse,
  RegisterResponse,
} from './generated/types.gen';

// Alias for backward-compat with components using the `Product` name
export type { ProductDto as Product } from './generated/types.gen';
