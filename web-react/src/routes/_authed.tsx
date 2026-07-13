import { createFileRoute, redirect, Outlet } from '@tanstack/react-router';
import { useAuthStore } from '@/stores/auth';
export const Route = createFileRoute('/_authed')({
    beforeLoad: async ({ location }) => {
        // Pas d'access token en mémoire (ex : rechargement de page) → tenter une
        // restauration silencieuse de session via le cookie refresh HttpOnly
        // avant de décider d'une redirection.
        const { isAuthenticated, restoreSession } = useAuthStore.getState();
        const authenticated = isAuthenticated || (await restoreSession());
        if (!authenticated)
            // eslint-disable-next-line @typescript-eslint/only-throw-error
            throw redirect({ to: '/login', search: { redirect: location.href } });
    },
    component: () => <Outlet />,
});
