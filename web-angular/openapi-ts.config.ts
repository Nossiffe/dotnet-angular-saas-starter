import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: './openapi.json',
  output: './src/app/api/generated',
  plugins: ['@hey-api/typescript'],
});
