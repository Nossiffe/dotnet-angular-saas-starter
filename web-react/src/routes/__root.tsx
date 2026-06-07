import { createRootRouteWithContext, Outlet, Link } from '@tanstack/react-router';
import type { RouterContext } from '@/router';
import { useAuthStore } from '@/stores/auth';

function Shell() {
    const { isAuthenticated, logout } = useAuthStore();
    return (
        <div>
            <header className="flex items-center justify-between border-b p-4">
                <Link to="/products" className="font-semibold">
                    Saas
                </Link>
                {isAuthenticated && (
                    <button onClick={logout} className="text-sm">
                        Déconnexion
                    </button>
                )}
            </header>
            <main className="p-4">
                <Outlet />
            </main>
        </div>
    );
}
export const Route = createRootRouteWithContext<RouterContext>()({ component: Shell });
