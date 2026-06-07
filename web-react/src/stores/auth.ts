import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

interface User {
    email: string;
}

export interface AuthState {
    accessToken: string | null;
    refreshToken: string | null;
    user: User | null;
    isAuthenticated: boolean;
    login: (accessToken: string, refreshToken: string, user: User) => void;
    setTokens: (accessToken: string, refreshToken: string) => void;
    /** Clear local auth state immediately (without server revocation). */
    clearAuth: () => void;
    /**
     * Revoke the refresh token server-side then clear local state.
     * Clears local state even if the server call fails.
     */
    logout: () => Promise<void>;
}

const clearState = {
    accessToken: null,
    refreshToken: null,
    user: null,
    isAuthenticated: false,
};

export const useAuthStore = create<AuthState>()(
    persist(
        (set, get) => ({
            accessToken: null,
            refreshToken: null,
            user: null,
            isAuthenticated: false,
            login: (accessToken, refreshToken, user) =>
                set({ accessToken, refreshToken, user, isAuthenticated: true }),
            setTokens: (accessToken, refreshToken) =>
                set({ accessToken, refreshToken, isAuthenticated: true }),
            clearAuth: () => set(clearState),
            logout: async () => {
                const rt = get().refreshToken;
                if (rt) {
                    try {
                        await fetch(`${import.meta.env.VITE_API_URL ?? ''}/api/v1/auth/logout`, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ refreshToken: rt }),
                        });
                    } catch {
                        // Network failure — still clear local state
                    }
                }
                set(clearState);
            },
        }),
        {
            name: 'auth',
            storage: createJSONStorage(() => localStorage),
            partialize: (s) => ({ refreshToken: s.refreshToken, user: s.user }),
            onRehydrateStorage: () => (state) => {
                if (state?.refreshToken) state.isAuthenticated = true;
            },
        },
    ),
);
