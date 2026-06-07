import { createRouter } from '@tanstack/react-router';
import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { routeTree } from './routeTree.gen';
import type { AuthState } from '@/stores/auth';

export interface RouterContext {
    queryClient: QueryClient;
    auth: AuthState;
}

interface ProblemDetails {
    title?: string;
    detail?: string;
    status?: number;
    errors?: Record<string, string[]>;
}

function parseProblemMessage(error: unknown): { message: string; status: number | undefined } {
    if (error && typeof error === 'object') {
        const pd = error as ProblemDetails;
        const status = typeof pd.status === 'number' ? pd.status : undefined;
        const message = pd.detail ?? pd.title ?? 'Une erreur est survenue.';
        return { message, status };
    }
    if (typeof error === 'string') {
        return { message: error, status: undefined };
    }
    return { message: 'Une erreur inattendue est survenue.', status: undefined };
}

function handleQueryError(error: unknown, queryKey?: unknown): void {
    const { message, status } = parseProblemMessage(error);
    if (status === 409) {
        toast.error('Cette ressource a été modifiée ailleurs, réessayez.', { duration: 6000 });
        // Invalidate the affected query so it refetches fresh data
        if (queryKey) {
            void queryClient.invalidateQueries({ queryKey: queryKey as unknown[] });
        }
        return;
    }
    // Don't double-toast 401 — the setup-client interceptor handles token refresh
    if (status === 401) return;
    toast.error(message, { duration: 6000 });
}

export const queryClient = new QueryClient({
    queryCache: new QueryCache({
        onError: (error, query) => {
            handleQueryError(error, query.queryKey);
        },
    }),
    mutationCache: new MutationCache({
        onError: (error) => {
            handleQueryError(error);
        },
    }),
});

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
