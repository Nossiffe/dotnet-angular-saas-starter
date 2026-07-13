// Single-flight PARTAGÉ entre tous les appelants concurrents de POST /api/v1/auth/refresh
// (restoreSession() du store d'auth au boot, ET l'intercepteur 401 du client Hey API dans
// setup-client.ts) : le refresh token est à usage unique côté serveur — deux appels
// concurrents double-dépenseraient le même token, et la détection de réutilisation côté
// serveur révoquerait alors TOUTES les sessions de l'utilisateur. Avant ce module, chaque
// appelant avait sa PROPRE promesse module-level (`restoring` / `refreshing`) : deux gardes
// indépendantes ne protègent rien l'une de l'autre.
//
// Ce module ne fait QUE l'appel réseau — pas d'effet de bord sur le store d'auth. Les
// sémantiques d'effet de bord diffèrent entre les deux appelants (cf. `stores/auth.ts` vs
// `setup-client.ts`, notamment sur l'échec réseau), donc chacun applique les siennes après
// avoir attendu `refreshOnce()`.
let refreshing: Promise<string | null> | null = null;

/**
 * Appelle `POST /api/v1/auth/refresh` au plus une fois par vague d'appels concurrents.
 *
 * Le refresh token ne transite jamais en JS : il vit dans le cookie HttpOnly `rt`
 * (Path=/api/v1/auth), envoyé automatiquement (même-origine).
 *
 * @returns le nouvel `accessToken`, ou `null` si le cookie refresh est absent/invalide (401
 * ou tout statut non-ok).
 * @throws si `fetch` rejette (erreur réseau) — PAS intercepté ici, propagé à l'appelant, qui
 * décide de son propre traitement.
 */
export function refreshOnce(): Promise<string | null> {
    refreshing ??= (async () => {
        try {
            const r = await fetch('/api/v1/auth/refresh', {
                method: 'POST',
                credentials: 'same-origin',
            });
            if (!r.ok) return null;
            const d = (await r.json()) as { accessToken: string };
            return d.accessToken;
        } finally {
            refreshing = null;
        }
    })();
    return refreshing;
}
