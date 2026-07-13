import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

interface User {
    email: string;
}

export interface AuthState {
    accessToken: string | null;
    user: User | null;
    isAuthenticated: boolean;
    login: (accessToken: string, user: User) => void;
    setTokens: (accessToken: string) => void;
    /** Clear local auth state immediately (without server revocation). */
    clearAuth: () => void;
    /**
     * Revoke the refresh token server-side (HttpOnly cookie `rt`, sent
     * automatically same-origin) then clear local state.
     * Clears local state even if the server call fails.
     */
    logout: () => Promise<void>;
    /**
     * Silent session restoration attempted at boot: tries the HttpOnly
     * refresh cookie. A 401 simply means "not logged in" and is not an error.
     * `user` stays persisted in localStorage for display only — real
     * authentication comes from this refresh call, never from localStorage.
     */
    restoreSession: () => Promise<boolean>;
}

const clearState = {
    accessToken: null,
    isAuthenticated: false,
};

export const useAuthStore = create<AuthState>()(
    persist(
        (set) => ({
            accessToken: null,
            user: null,
            isAuthenticated: false,
            login: (accessToken, user) => set({ accessToken, user, isAuthenticated: true }),
            setTokens: (accessToken) => set({ accessToken, isAuthenticated: true }),
            clearAuth: () => set(clearState),
            logout: async () => {
                try {
                    await fetch('/api/v1/auth/logout', {
                        method: 'POST',
                        credentials: 'same-origin',
                    });
                } catch {
                    // Network failure — still clear local state
                }
                set(clearState);
            },
            restoreSession: async () => {
                try {
                    const r = await fetch('/api/v1/auth/refresh', {
                        method: 'POST',
                        credentials: 'same-origin',
                    });
                    if (!r.ok) {
                        set(clearState);
                        return false;
                    }
                    const d = (await r.json()) as { accessToken: string };
                    set({ accessToken: d.accessToken, isAuthenticated: true });
                    return true;
                } catch {
                    set(clearState);
                    return false;
                }
            },
        }),
        {
            name: 'auth',
            storage: createJSONStorage(() => localStorage),
            partialize: (s) => ({ user: s.user }),
        },
    ),
);
