import type { CreateClientConfig } from '@hey-api/client-fetch';

export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  baseUrl: import.meta.env.VITE_API_URL,
});
