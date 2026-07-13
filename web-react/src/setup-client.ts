import { client } from '@/client/client.gen';
import { useAuthStore } from '@/stores/auth';

// (a) Bearer on each request
client.interceptors.request.use((request) => {
    const token = useAuthStore.getState().accessToken;
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return request;
});

// (b) 401 → refresh single-flight → retry (GET/DELETE safe; POST/PUT/PATCH skipped when body consumed)
let refreshing: Promise<string | null> | null = null;

client.interceptors.response.use(async (response, request) => {
    if (response.status !== 401) return response;
    // Guard against refresh loop (the /api/v1/auth/refresh call itself returns 401)
    if (request.url.endsWith('/api/v1/auth/refresh')) {
        useAuthStore.getState().clearAuth();
        return response;
    }
    refreshing ??= (async () => {
        // Le refresh token ne transite jamais en JS : il vit dans le cookie
        // HttpOnly `rt` (Path=/api/v1/auth), envoyé automatiquement (même-origine).
        const r = await fetch('/api/v1/auth/refresh', {
            method: 'POST',
            credentials: 'same-origin',
        });
        if (!r.ok) {
            useAuthStore.getState().clearAuth();
            return null;
        }
        const d = (await r.json()) as { accessToken: string };
        useAuthStore.getState().setTokens(d.accessToken);
        return d.accessToken;
    })();
    let newToken: string | null;
    try {
        newToken = await refreshing;
    } finally {
        refreshing = null;
    }
    if (!newToken) return response;
    // POST/PUT/PATCH body can't be replayed once consumed → let the mutation's onError handle the 401
    if (request.bodyUsed) return response;
    const retried = new Request(request, { headers: new Headers(request.headers) });
    retried.headers.set('Authorization', `Bearer ${newToken}`);
    return fetch(retried);
});
