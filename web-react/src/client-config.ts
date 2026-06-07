import type { CreateClientConfig } from '@/client/client';

export const createClientConfig: CreateClientConfig = (config) => ({
    ...config,
    baseUrl: import.meta.env.VITE_API_URL as string,
});
