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
    logout: () => void;
}

export const useAuthStore = create<AuthState>()(
    persist(
        (set) => ({
            accessToken: null,
            refreshToken: null,
            user: null,
            isAuthenticated: false,
            login: (accessToken, refreshToken, user) =>
                set({ accessToken, refreshToken, user, isAuthenticated: true }),
            setTokens: (accessToken, refreshToken) =>
                set({ accessToken, refreshToken, isAuthenticated: true }),
            logout: () =>
                set({ accessToken: null, refreshToken: null, user: null, isAuthenticated: false }),
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
