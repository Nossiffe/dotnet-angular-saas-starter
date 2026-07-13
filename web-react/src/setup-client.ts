import { client } from '@/client/client.gen';
import { useAuthStore } from '@/stores/auth';
import { refreshOnce } from '@/lib/refresh';

// (a) Bearer on each request
client.interceptors.request.use((request) => {
    const token = useAuthStore.getState().accessToken;
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return request;
});

// (b) 401 → refresh single-flight (partagé avec restoreSession(), cf. src/lib/refresh.ts) →
// retry (GET/DELETE safe; POST/PUT/PATCH skipped when body consumed)
client.interceptors.response.use(async (response, request) => {
    if (response.status !== 401) return response;
    // Guard against refresh loop (the /api/v1/auth/refresh call itself returns 401)
    if (request.url.endsWith('/api/v1/auth/refresh')) {
        useAuthStore.getState().clearAuth();
        return response;
    }
    const newToken = await refreshOnce();
    if (newToken === null) {
        useAuthStore.getState().clearAuth();
        return response;
    }
    useAuthStore.getState().setTokens(newToken);
    // POST/PUT/PATCH body can't be replayed once consumed → let the mutation's onError handle the 401
    if (request.bodyUsed) return response;
    const retried = new Request(request, { headers: new Headers(request.headers) });
    retried.headers.set('Authorization', `Bearer ${newToken}`);
    return fetch(retried);
});
