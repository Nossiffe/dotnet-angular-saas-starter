import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { toast } from 'sonner';
import { postApiV1AuthResetPassword } from '@/client/sdk.gen';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Form,
    FormControl,
    FormField,
    FormItem,
    FormLabel,
    FormMessage,
} from '@/components/ui/form';
import { Route } from '@/routes/reset-password';

const schema = z.object({
    newPassword: z.string().min(12, 'Min 12 caractères'),
});
type Values = z.infer<typeof schema>;

export function ResetPasswordPage() {
    const navigate = useNavigate();
    const { userId, token } = Route.useSearch();
    const form = useForm<Values>({
        resolver: zodResolver(schema),
        defaultValues: { newPassword: '' },
    });

    const resetMut = useMutation<
        unknown,
        unknown,
        { userId: string; token: string; newPassword: string }
    >({
        meta: { skipGlobalError: true },
        mutationFn: async (vars) => {
            const { data } = await postApiV1AuthResetPassword({
                body: vars,
                throwOnError: true,
            });
            return data;
        },
        onSuccess: () => {
            toast.success('Mot de passe réinitialisé avec succès.');
            void navigate({ to: '/login' });
        },
        onError: (error: unknown) => {
            const err = error as { status?: number } | null;
            if (err?.status === 400) {
                form.setError('root', { message: 'Lien invalide ou expiré.' });
            } else {
                form.setError('root', { message: 'Une erreur est survenue, veuillez réessayer.' });
            }
        },
    });

    if (!userId || !token) {
        return (
            <div className="mx-auto max-w-sm space-y-4">
                <h1 className="text-xl font-semibold">Réinitialiser le mot de passe</h1>
                <p className="text-sm text-destructive">
                    Lien invalide ou expiré.{' '}
                    <a href="/forgot-password" className="underline">
                        Demander un nouveau lien
                    </a>
                </p>
            </div>
        );
    }

    return (
        <Form {...form}>
            <form
                onSubmit={(e) => {
                    void form.handleSubmit((v) =>
                        resetMut.mutate({
                            userId,
                            token,
                            newPassword: v.newPassword,
                        }),
                    )(e);
                }}
                className="mx-auto max-w-sm space-y-4"
            >
                <h1 className="text-xl font-semibold">Réinitialiser le mot de passe</h1>
                <FormField
                    control={form.control}
                    name="newPassword"
                    render={({ field }) => (
                        <FormItem>
                            <FormLabel>Nouveau mot de passe</FormLabel>
                            <FormControl>
                                <Input type="password" {...field} />
                            </FormControl>
                            <FormMessage />
                        </FormItem>
                    )}
                />
                {form.formState.errors.root && (
                    <p className="text-sm text-destructive">{form.formState.errors.root.message}</p>
                )}
                <Button type="submit" disabled={resetMut.isPending} className="w-full">
                    {resetMut.isPending ? 'Enregistrement…' : 'Enregistrer le mot de passe'}
                </Button>
            </form>
        </Form>
    );
}
