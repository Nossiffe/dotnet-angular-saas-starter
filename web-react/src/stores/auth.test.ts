import { describe, it, expect, beforeEach } from 'vitest';
import { useAuthStore } from './auth';

describe('auth store', () => {
    beforeEach(() => useAuthStore.getState().logout());
    it('login authentifie et logout nettoie', () => {
        useAuthStore.getState().login('at', 'rt', { email: 'a@b.com' });
        expect(useAuthStore.getState().isAuthenticated).toBe(true);
        expect(useAuthStore.getState().accessToken).toBe('at');
        useAuthStore.getState().logout();
        expect(useAuthStore.getState().isAuthenticated).toBe(false);
        expect(useAuthStore.getState().accessToken).toBeNull();
    });
});
