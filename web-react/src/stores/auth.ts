import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import { toast } from 'sonner';
import { refreshOnce } from '@/lib/refresh';

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

// Partagée entre tous les appelants concurrents de restoreSession() (ex. plusieurs
// beforeLoad de route montés en même temps) : leur évite de relancer chacun un appel à
// refreshOnce() (qui, lui, dédoublonne aussi avec l'intercepteur 401 de setup-client.ts —
// cf. `src/lib/refresh.ts`).
let restoring: Promise<boolean> | null = null;

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
                    // Pas de Bearer requis (endpoint AllowAnonymous — la credential réelle est le
                    // cookie HttpOnly `rt`, envoyé automatiquement same-origin).
                    const r = await fetch('/api/v1/auth/logout', {
                        method: 'POST',
                        credentials: 'same-origin',
                    });
                    if (!r.ok) {
                        // Échec NON réseau (ex. 500) : la révocation serveur a pu ne pas avoir lieu.
                        // On ne l'avale pas silencieusement — on le surface via le canal toast existant —
                        // mais on efface quand même l'état local : rester "connecté" côté UI alors que
                        // l'utilisateur a demandé la déconnexion serait pire.
                        toast.error(
                            'La déconnexion côté serveur a échoué (session locale effacée).',
                        );
                    }
                } catch {
                    // Échec réseau : rien à surfacer de plus utile, on efface l'état local.
                }
                set(clearState);
            },
            restoreSession: () => {
                restoring ??= (async () => {
                    try {
                        const accessToken = await refreshOnce();
                        if (accessToken === null) {
                            set(clearState);
                            return false;
                        }
                        set({ accessToken, isAuthenticated: true });
                        return true;
                    } catch {
                        set(clearState);
                        return false;
                    } finally {
                        restoring = null;
                    }
                })();
                return restoring;
            },
        }),
        {
            name: 'auth',
            storage: createJSONStorage(() => localStorage),
            partialize: (s) => ({ user: s.user }),
        },
    ),
);
