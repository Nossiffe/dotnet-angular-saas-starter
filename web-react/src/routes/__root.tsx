import { createRootRouteWithContext, Outlet, Link, useRouter } from '@tanstack/react-router';
import { Toaster } from '@/components/ui/sonner';
import type { RouterContext } from '@/router';
import { useAuthStore } from '@/stores/auth';

function Shell() {
    const { isAuthenticated, logout } = useAuthStore();
    const router = useRouter();

    const handleLogout = () => {
        void logout().then(() => router.navigate({ to: '/login' }));
    };

    return (
        <div>
            <header className="flex items-center justify-between border-b p-4">
                <Link to="/products" className="font-semibold">
                    Saas
                </Link>
                {isAuthenticated && (
                    <button onClick={handleLogout} className="text-sm">
                        Déconnexion
                    </button>
                )}
            </header>
            <main className="p-4">
                <Outlet />
            </main>
            <Toaster />
        </div>
    );
}
export const Route = createRootRouteWithContext<RouterContext>()({ component: Shell });
