import type { CreateClientConfig } from '@/client/client';

// Même-origine (proxy Vite en dev, reverse-proxy en prod) — pas d'URL absolue.
export const createClientConfig: CreateClientConfig = (config) => ({
    ...config,
    baseUrl: '',
});
