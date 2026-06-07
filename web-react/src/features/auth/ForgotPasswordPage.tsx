import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { postApiV1AuthForgotPasswordMutation } from '@/client/@tanstack/react-query.gen';
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
});
type Values = z.infer<typeof schema>;

export function ForgotPasswordPage() {
    const [sent, setSent] = useState(false);
    const form = useForm<Values>({
        resolver: zodResolver(schema),
        defaultValues: { email: '' },
    });

    const forgotMut = useMutation({
        ...postApiV1AuthForgotPasswordMutation(),
        meta: { skipGlobalError: true },
        onSuccess: () => setSent(true),
        // Anti-enumeration: always show neutral message regardless of result
        onError: () => setSent(true),
    });

    if (sent) {
        return (
            <div className="mx-auto max-w-sm space-y-4">
                <h1 className="text-xl font-semibold">Mot de passe oublié</h1>
                <p className="text-sm text-muted-foreground">
                    Si un compte existe pour cette adresse, un email a été envoyé.
                </p>
                <Link to="/login" className="text-sm underline">
                    Retour à la connexion
                </Link>
            </div>
        );
    }

    return (
        <Form {...form}>
            <form
                onSubmit={(e) => {
                    void form.handleSubmit((v) => forgotMut.mutate({ body: { email: v.email } }))(
                        e,
                    );
                }}
                className="mx-auto max-w-sm space-y-4"
            >
                <h1 className="text-xl font-semibold">Mot de passe oublié</h1>
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
                <Button type="submit" disabled={forgotMut.isPending} className="w-full">
                    {forgotMut.isPending ? 'Envoi…' : 'Envoyer'}
                </Button>
                <p className="text-sm">
                    <Link to="/login" className="underline">
                        Retour à la connexion
                    </Link>
                </p>
            </form>
        </Form>
    );
}
