import { createRouter } from '@tanstack/react-router';
import { QueryClient } from '@tanstack/react-query';
import { routeTree } from './routeTree.gen';
import type { AuthState } from '@/stores/auth';

export interface RouterContext {
  queryClient: QueryClient;
  auth: AuthState;
}
export const queryClient = new QueryClient();
export const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
  context: { queryClient, auth: undefined! },
  scrollRestoration: true,
});
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
