import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { useNavigate, useSearch, Link } from '@tanstack/react-router';
import { postAuthLoginMutation } from '@/client/@tanstack/react-query.gen';
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

interface AuthResponse {
  accessToken: string;
  refreshToken: string;
}

export function LoginPage() {
  const navigate = useNavigate();
  const search = useSearch({ from: '/login' });
  const login = useAuthStore((s) => s.login);
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });

  const loginMut = useMutation({
    ...postAuthLoginMutation(),
    onSuccess: (data, vars) => {
      const resp = data as AuthResponse;
      login(resp.accessToken, resp.refreshToken, {
        email: (vars.body as { email: string }).email,
      });
      void navigate({ to: search.redirect ?? '/products' });
    },
    onError: () => form.setError('root', { message: 'Identifiants invalides' }),
  });

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit((v) =>
          loginMut.mutate({ body: { email: v.email, password: v.password, tenantId: null } }),
        )}
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
          Pas de compte ?{' '}
          <Link to="/register" className="underline">
            Créer un compte
          </Link>
        </p>
      </form>
    </Form>
  );
}
