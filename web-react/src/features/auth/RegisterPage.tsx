import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { useNavigate, Link } from '@tanstack/react-router';
import { postAuthRegisterMutation } from '@/client/@tanstack/react-query.gen';
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
    password: z.string().min(12, 'Min 12 caractères'),
});
type Values = z.infer<typeof schema>;

export function RegisterPage() {
    const navigate = useNavigate();
    const form = useForm<Values>({
        resolver: zodResolver(schema),
        defaultValues: { email: '', password: '' },
    });

    const registerMut = useMutation({
        ...postAuthRegisterMutation(),
        onSuccess: () => {
            void navigate({ to: '/login' });
        },
        onError: () => form.setError('root', { message: 'Erreur lors de la création du compte' }),
    });

    return (
        <Form {...form}>
            <form
                onSubmit={(e) => {
                    void form.handleSubmit((v) => registerMut.mutate({ body: v }))(e);
                }}
                className="mx-auto max-w-sm space-y-4"
            >
                <h1 className="text-xl font-semibold">Créer un compte</h1>
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
                <Button type="submit" disabled={registerMut.isPending} className="w-full">
                    {registerMut.isPending ? 'Inscription…' : 'Créer un compte'}
                </Button>
                <p className="text-sm">
                    Déjà un compte ?{' '}
                    <Link to="/login" className="underline">
                        Se connecter
                    </Link>
                </p>
            </form>
        </Form>
    );
}
