import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from '@tanstack/react-router';
import { QueryClientProvider } from '@tanstack/react-query';
import { router, queryClient } from './router';
import { useAuthStore } from '@/stores/auth';
import '@/setup-client';
import '@/index.css';

function App() {
    const auth = useAuthStore();
    return <RouterProvider router={router} context={{ auth }} />;
}
createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <App />
        </QueryClientProvider>
    </StrictMode>,
);
