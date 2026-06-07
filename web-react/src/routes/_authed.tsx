import { createFileRoute, redirect, Outlet } from '@tanstack/react-router';
export const Route = createFileRoute('/_authed')({
  beforeLoad: ({ context, location }) => {
    if (!context.auth.isAuthenticated)
      throw redirect({ to: '/login', search: { redirect: location.href } });
  },
  component: () => <Outlet />,
});
