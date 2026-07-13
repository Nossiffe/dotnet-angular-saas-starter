import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useAuthStore } from './auth';

// Mock fetch for logout/refresh calls
const mockFetch = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve({}) });
vi.stubGlobal('fetch', mockFetch);

describe('auth store', () => {
    beforeEach(() => {
        useAuthStore.getState().clearAuth();
        mockFetch.mockClear();
        localStorage.clear();
    });

    it('login authentifie et clearAuth nettoie', () => {
        useAuthStore.getState().login('at', { email: 'a@b.com' });
        expect(useAuthStore.getState().isAuthenticated).toBe(true);
        expect(useAuthStore.getState().accessToken).toBe('at');
        useAuthStore.getState().clearAuth();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().accessToken).toBeNull();
    });

    it("logout révoque la session serveur (cookie refresh, body vide) puis nettoie l'état", async () => {
        useAuthStore.getState().login('at', { email: 'a@b.com' });
        await useAuthStore.getState().logout();
        expect(mockFetch).toHaveBeenCalledOnce();
        const [url, opts] = mockFetch.mock.calls[0] as [string, RequestInit];
        expect(url).toBe('/api/v1/auth/logout');
        expect(opts.method).toBe('POST');
        expect(opts.credentials).toBe('same-origin');
        expect(opts.body).toBeUndefined();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().accessToken).toBeNull();
    });

    it("logout nettoie l'état même si le serveur échoue", async () => {
        mockFetch.mockRejectedValueOnce(new Error('Network error'));
        useAuthStore.getState().login('at', { email: 'a@b.com' });
        await useAuthStore.getState().logout();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
    });

    it('restoreSession() authentifie silencieusement si le cookie refresh est valide', async () => {
        mockFetch.mockResolvedValueOnce({
            ok: true,
            json: () => Promise.resolve({ accessToken: 'new-at' }),
        });
        const ok = await useAuthStore.getState().restoreSession();
        expect(ok).toBe(true);
        expect(useAuthStore.getState().isAuthenticated).toBe(true);
        expect(useAuthStore.getState().accessToken).toBe('new-at');
        const [url, opts] = mockFetch.mock.calls[0] as [string, RequestInit];
        expect(url).toBe('/api/v1/auth/refresh');
        expect(opts.method).toBe('POST');
        expect(opts.credentials).toBe('same-origin');
        expect(opts.body).toBeUndefined();
    });

    it('restoreSession() renvoie false sans erreur si le cookie refresh est absent (401)', async () => {
        mockFetch.mockResolvedValueOnce({ ok: false, status: 401 });
        const ok = await useAuthStore.getState().restoreSession();
        expect(ok).toBe(false);
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().accessToken).toBeNull();
    });

    it("le localStorage persisté ('auth') ne contient aucun refreshToken — seul `user` est conservé", () => {
        useAuthStore.getState().login('at', { email: 'a@b.com' });
        const raw = localStorage.getItem('auth');
        expect(raw).not.toBeNull();
        const persisted = JSON.parse(raw!) as { state: Record<string, unknown> };
        expect(persisted.state).not.toHaveProperty('refreshToken');
        expect(persisted.state).not.toHaveProperty('accessToken');
        expect(persisted.state).toEqual({ user: { email: 'a@b.com' } });
    });
});
