import { Observable, Subject, defer, finalize, firstValueFrom } from 'rxjs';
import { afterEach, describe, expect, it } from 'vitest';
import { withCrossTabLock } from './cross-tab-lock';
import { FakeLocks } from '../../testing/fake-locks';

// Une tâche observable dont le test décide la fin, et qui dit si elle a démarré et pris fin.
function work() {
  const result = new Subject<string>();
  const state = { started: false, ended: false };
  const observable = defer(() => {
    state.started = true;
    return result.pipe(finalize(() => (state.ended = true)));
  });
  return { result, state, observable };
}

// Les verrous sont accordés de façon asynchrone, comme dans un navigateur.
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('withCrossTabLock', () => {
  let uninstall: (() => void) | undefined;
  afterEach(() => uninstall?.());

  function installLocks(): FakeLocks {
    const locks = new FakeLocks();
    uninstall = locks.install();
    return locks;
  }

  it('runs the work at once without the Web Locks API', () => {
    const task = work();
    const values: string[] = [];
    withCrossTabLock('lock', task.observable).subscribe((value) => values.push(value));

    expect(task.state.started).toBe(true);
    task.result.next('done');
    expect(values).toEqual(['done']);
  });

  it('runs one work at a time, in the order asked, the next once the previous has ended', async () => {
    installLocks();
    const first = work();
    const second = work();
    const values: string[] = [];
    withCrossTabLock('lock', first.observable).subscribe((value) => values.push(value));
    withCrossTabLock('lock', second.observable).subscribe((value) => values.push(value));
    await settle();

    expect(first.state.started).toBe(true);
    expect(second.state.started).toBe(false);

    first.result.next('first');
    first.result.complete();
    await settle();
    expect(second.state.started).toBe(true);

    second.result.next('second');
    expect(values).toEqual(['first', 'second']);
  });

  it('releases the lock when the work fails, and passes the error on', async () => {
    const locks = installLocks();
    const first = work();
    const second = work();
    let error: unknown;
    withCrossTabLock('lock', first.observable).subscribe({
      error: (err: unknown) => (error = err),
    });
    withCrossTabLock('lock', second.observable).subscribe();
    await settle();

    first.result.error(new Error('refused'));
    await settle();

    expect(error).toEqual(new Error('refused'));
    expect(second.state.started).toBe(true);
    expect(locks.held.has('lock')).toBe(true);
  });

  it('gives up the wait without running the work when unsubscribed before its turn', async () => {
    const locks = installLocks();
    const first = work();
    const second = work();
    withCrossTabLock('lock', first.observable).subscribe();
    const waiting = withCrossTabLock('lock', second.observable).subscribe();
    await settle();
    expect(locks.waiting('lock')).toBe(1);

    waiting.unsubscribe();
    expect(locks.waiting('lock')).toBe(0);
    first.result.complete();
    await settle();

    expect(second.state.started).toBe(false);
    expect(locks.held.has('lock')).toBe(false);
  });

  it('ends the work and releases the lock when unsubscribed while running', async () => {
    const locks = installLocks();
    const task = work();
    const running = withCrossTabLock('lock', task.observable).subscribe();
    await settle();
    expect(task.state.started).toBe(true);

    running.unsubscribe();
    await settle();

    expect(task.state.ended).toBe(true);
    expect(locks.held.has('lock')).toBe(false);
  });

  // Une valeur émise pendant subscribe(), et un abonné qui part aussitôt (firstValueFrom, take(1)).
  it('ends the work and releases the lock when the subscriber leaves on a value emitted at once', async () => {
    const locks = installLocks();
    let ended = false;
    const endless = new Observable<string>((subscriber) => {
      subscriber.next('now');
      return () => (ended = true);
    });

    const value = await firstValueFrom(withCrossTabLock('lock', endless));
    await settle();

    expect(value).toBe('now');
    expect(ended).toBe(true);
    expect(locks.held.has('lock')).toBe(false);
  });

  it('passes on a refused lock request', async () => {
    const refusal = new DOMException('No locks in this context.', 'SecurityError');
    Object.defineProperty(navigator, 'locks', {
      value: { request: () => Promise.reject(refusal) },
      configurable: true,
    });
    uninstall = () => Reflect.deleteProperty(navigator, 'locks');
    const task = work();
    let error: unknown;
    withCrossTabLock('lock', task.observable).subscribe({ error: (err: unknown) => (error = err) });
    await settle();

    expect(error).toBe(refusal);
    expect(task.state.started).toBe(false);
  });

  it('does not run the work when unsubscribed between the grant and its turn', async () => {
    const locks = installLocks();
    const task = work();
    // Le verrou est réservé à la demande, le rappel vient après : le désabonnement passe entre.
    withCrossTabLock('lock', task.observable).subscribe().unsubscribe();
    await settle();

    expect(task.state.started).toBe(false);
    expect(locks.held.has('lock')).toBe(false);
  });
});
