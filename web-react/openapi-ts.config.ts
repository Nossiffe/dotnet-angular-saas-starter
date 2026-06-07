import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: './openapi.json',
  output: { path: './src/client', format: 'prettier' },
  plugins: [
    { name: '@hey-api/client-fetch', runtimeConfigPath: './src/client-config.ts' },
    '@hey-api/sdk',
    '@hey-api/typescript',
    { name: '@tanstack/react-query', queryOptions: true, mutationOptions: true },
  ],
});
