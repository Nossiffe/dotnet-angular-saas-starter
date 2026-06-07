import { createFileRoute, redirect, Outlet } from '@tanstack/react-router';
import { useAuthStore } from '@/stores/auth';
export const Route = createFileRoute('/_authed')({
    beforeLoad: ({ location }) => {
        if (!useAuthStore.getState().isAuthenticated)
            // eslint-disable-next-line @typescript-eslint/only-throw-error
            throw redirect({ to: '/login', search: { redirect: location.href } });
    },
    component: () => <Outlet />,
});
