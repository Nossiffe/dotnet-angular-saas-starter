import { Observable, Subscription, finalize } from 'rxjs';

/**
 * Exécute `work` en tenant le verrou Web Locks `name` : entre les onglets et workers de l'origine,
 * un seul à la fois, les autres attendent leur tour. Le verrou est rendu quand `work` se termine,
 * en succès ou en erreur, ou quand l'abonné se désabonne.
 *
 * Se désabonner pendant l'attente renonce au verrou sans exécuter `work`. Sans l'API Web Locks
 * (contexte non sécurisé, navigateur ancien, jsdom), `work` s'exécute tout de suite, sans verrou.
 */
export function withCrossTabLock<T>(name: string, work: Observable<T>): Observable<T> {
  if (typeof navigator === 'undefined' || !('locks' in navigator)) return work;
  const locks = navigator.locks;

  return new Observable<T>((subscriber) => {
    const waiting = new AbortController();
    let running: Subscription | undefined;

    locks
      .request(name, { signal: waiting.signal }, () => {
        // Verrou obtenu juste après un désabonnement : rien à exécuter, il est rendu aussitôt.
        if (subscriber.closed) return Promise.resolve();
        return new Promise<void>((release) => {
          running = work.pipe(finalize(release)).subscribe({
            next: (value) => subscriber.next(value),
            error: (err: unknown) => subscriber.error(err),
            complete: () => subscriber.complete(),
          });
          // Un abonné qui s'est désabonné pendant subscribe() (une valeur émise tout de suite, et
          // take(1) ou firstValueFrom) n'a pas pu arrêter `work` : `running` n'existait pas encore.
          if (subscriber.closed) running.unsubscribe();
        });
      })
      // Un refus de la demande de verrou (SecurityError, par exemple) est transmis. Après un
      // désabonnement, l'AbortError de l'attente abandonnée arrive à un abonné fermé, qui l'ignore.
      .catch((err: unknown) => subscriber.error(err));

    return () => {
      waiting.abort();
      running?.unsubscribe();
    };
  });
}
