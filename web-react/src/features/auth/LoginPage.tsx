import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { useNavigate, useSearch, Link } from '@tanstack/react-router';
import { postApiV1AuthLoginMutation } from '@/client/@tanstack/react-query.gen';
import { useAuthStore } from '@/stores/auth';
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

const schema = z.object({
    email: z.email('Email invalide'),
    password: z.string().min(8, 'Min 8 caractères'),
});
type Values = z.infer<typeof schema>;

export function LoginPage() {
    const navigate = useNavigate();
    const search = useSearch({ from: '/login' });
    const login = useAuthStore((s) => s.login);
    const form = useForm<Values>({
        resolver: zodResolver(schema),
        defaultValues: { email: '', password: '' },
    });

    const loginMut = useMutation({
        ...postApiV1AuthLoginMutation(),
        onSuccess: (data, vars) => {
            login(data.accessToken, {
                email: (vars.body as { email: string }).email,
            });
            const safeTo =
                typeof search.redirect === 'string' && /^\/(?![/\\])/.test(search.redirect)
                    ? search.redirect
                    : '/products';
            void navigate({ to: safeTo });
        },
        onError: () => form.setError('root', { message: 'Identifiants invalides' }),
    });

    return (
        <Form {...form}>
            <form
                onSubmit={(e) => {
                    void form.handleSubmit((v) =>
                        loginMut.mutate({
                            body: { email: v.email, password: v.password, tenantId: null },
                        }),
                    )(e);
                }}
                className="mx-auto max-w-sm space-y-4"
            >
                <h1 className="text-xl font-semibold">Connexion</h1>
                <FormField
                    control={form.control}
                    name="email"
                    render={({ field }) => (
                        <FormItem>
                            <FormLabel>Email</FormLabel>
                            <FormControl>
                                <Input type="email" {...field} />
                            </FormControl>
                            <FormMessage />
                        </FormItem>
                    )}
                />
                <FormField
                    control={form.control}
                    name="password"
                    render={({ field }) => (
                        <FormItem>
                            <FormLabel>Mot de passe</FormLabel>
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
                <Button type="submit" disabled={loginMut.isPending} className="w-full">
                    {loginMut.isPending ? 'Connexion…' : 'Se connecter'}
                </Button>
                <p className="text-sm">
                    <Link to="/forgot-password" className="underline">
                        Mot de passe oublié ?
                    </Link>
                </p>
                <p className="text-sm">
                    Pas de compte ?{' '}
                    <Link to="/register" className="underline">
                        Créer un compte
                    </Link>
                </p>
            </form>
        </Form>
    );
}
