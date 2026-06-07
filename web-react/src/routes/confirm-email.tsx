import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';
import { ConfirmEmailPage } from '@/features/auth/ConfirmEmailPage';

export const Route = createFileRoute('/confirm-email')({
    validateSearch: z.object({
        userId: z.string().optional(),
        token: z.string().optional(),
    }),
    component: ConfirmEmailPage,
});
