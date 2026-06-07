// Runtime config for the generated Hey API client.
// Uses structural compatibility (no explicit type import) to avoid
// version conflicts between openapi-ts v0.98 bundled internals and
// @hey-api/client-fetch v0.13.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const createClientConfig = (config: any) => ({
  ...config,
  baseUrl: import.meta.env.VITE_API_URL as string,
});
