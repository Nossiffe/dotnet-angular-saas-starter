import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useAuthStore } from './auth';

// Mock fetch for logout revocation tests
const mockFetch = vi.fn().mockResolvedValue({ ok: true });
vi.stubGlobal('fetch', mockFetch);

describe('auth store', () => {
    beforeEach(() => {
        useAuthStore.getState().clearAuth();
        mockFetch.mockClear();
    });

    it('login authentifie et clearAuth nettoie', () => {
        useAuthStore.getState().login('at', 'rt', { email: 'a@b.com' });
        expect(useAuthStore.getState().isAuthenticated).toBe(true);
        expect(useAuthStore.getState().accessToken).toBe('at');
        useAuthStore.getState().clearAuth();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().accessToken).toBeNull();
    });

    it("logout révoque le token serveur puis nettoie l'état", async () => {
        useAuthStore.getState().login('at', 'rt', { email: 'a@b.com' });
        await useAuthStore.getState().logout();
        expect(mockFetch).toHaveBeenCalledOnce();
        const [url, opts] = mockFetch.mock.calls[0] as [string, RequestInit];
        expect(url).toContain('/api/v1/auth/logout');
        expect(JSON.parse(opts.body as string)).toEqual({ refreshToken: 'rt' });
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().accessToken).toBeNull();
    });

    it("logout nettoie l'état même si le serveur échoue", async () => {
        mockFetch.mockRejectedValueOnce(new Error('Network error'));
        useAuthStore.getState().login('at', 'rt', { email: 'a@b.com' });
        await useAuthStore.getState().logout();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
    });
});
