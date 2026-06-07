import { useEffect, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { postApiV1AuthConfirmEmailMutation } from '@/client/@tanstack/react-query.gen';
import { Route } from '@/routes/confirm-email';

type Status = 'pending' | 'success' | 'error' | 'invalid';

export function ConfirmEmailPage() {
    const { userId, token } = Route.useSearch();
    const [status, setStatus] = useState<Status>(() => (!userId || !token ? 'invalid' : 'pending'));

    const confirmMut = useMutation({
        ...postApiV1AuthConfirmEmailMutation(),
        onSuccess: () => setStatus('success'),
        onError: () => setStatus('error'),
    });

    useEffect(() => {
        if (userId && token) {
            confirmMut.mutate({ body: { userId, token } });
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    return (
        <div className="mx-auto max-w-sm space-y-4">
            <h1 className="text-xl font-semibold">Confirmation de l&apos;adresse email</h1>
            {status === 'pending' && (
                <p className="text-sm text-muted-foreground">Vérification en cours…</p>
            )}
            {status === 'success' && (
                <>
                    <p className="text-sm text-green-600">
                        Votre adresse email a été confirmée avec succès.
                    </p>
                    <Link to="/login" className="text-sm underline">
                        Se connecter
                    </Link>
                </>
            )}
            {status === 'error' && (
                <>
                    <p className="text-sm text-destructive">
                        Le lien de confirmation est invalide ou a expiré.
                    </p>
                    <Link to="/login" className="text-sm underline">
                        Retour à la connexion
                    </Link>
                </>
            )}
            {status === 'invalid' && (
                <>
                    <p className="text-sm text-destructive">Lien invalide.</p>
                    <Link to="/login" className="text-sm underline">
                        Retour à la connexion
                    </Link>
                </>
            )}
        </div>
    );
}
