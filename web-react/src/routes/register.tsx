import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { RegisterPage } from '@/features/auth/RegisterPage';
export const Route = createFileRoute('/register')({
    validateSearch: z.object({ redirect: z.string().optional() }),
    component: RegisterPage,
});
