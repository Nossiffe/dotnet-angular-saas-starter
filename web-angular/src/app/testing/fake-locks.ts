/**
 * Un LockManager minimal pour jsdom, qui n'a pas l'API Web Locks : des verrous exclusifs, accordés
 * dans l'ordre des demandes, de façon asynchrone comme dans un navigateur. `install()` le pose sur
 * `navigator.locks` et rend une fonction qui le retire.
 */
export class FakeLocks {
  /** Noms des verrous tenus. */
  readonly held = new Set<string>();
  private readonly queues = new Map<string, (() => void)[]>();

  /** Demandes en attente pour ce verrou. */
  waiting(name: string): number {
    return this.queues.get(name)?.length ?? 0;
  }

  request(
    name: string,
    options: { signal?: AbortSignal },
    callback: () => Promise<unknown>,
  ): Promise<unknown> {
    return new Promise((resolve, reject) => {
      const queue = this.queues.get(name) ?? [];
      this.queues.set(name, queue);
      // Le verrou est réservé dès la demande, le rappel appelé ensuite : deux demandes du même
      // instant ne l'obtiennent pas toutes les deux.
      const grant = () => {
        callback()
          .then(resolve, reject)
          .finally(() => {
            const next = queue.shift();
            if (next) queueMicrotask(next);
            else this.held.delete(name);
          });
      };
      options.signal?.addEventListener('abort', () => {
        const index = queue.indexOf(grant);
        if (index === -1) return;
        queue.splice(index, 1);
        reject(new DOMException('The lock request was aborted.', 'AbortError'));
      });
      if (this.held.has(name)) {
        queue.push(grant);
      } else {
        this.held.add(name);
        queueMicrotask(grant);
      }
    });
  }

  install(): () => void {
    Object.defineProperty(navigator, 'locks', { value: this, configurable: true });
    return () => {
      Reflect.deleteProperty(navigator, 'locks');
    };
  }
}
