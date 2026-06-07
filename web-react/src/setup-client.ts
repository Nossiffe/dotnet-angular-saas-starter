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
        useAuthStore.getState().logout();
        return response;
    }
    refreshing ??= (async () => {
        const { refreshToken, setTokens, logout } = useAuthStore.getState();
        if (!refreshToken) {
            logout();
            return null;
        }
        const r = await fetch(`${import.meta.env.VITE_API_URL}/api/v1/auth/refresh`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken }),
        });
        if (!r.ok) {
            logout();
            return null;
        }
        const d = (await r.json()) as { accessToken: string; refreshToken: string };
        setTokens(d.accessToken, d.refreshToken);
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
