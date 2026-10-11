import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AuthService, SESSION_COOKIE_LOCK } from './auth.service';
import { fakeAccessToken, ORG_ID, OWNER_ID } from '../../testing/fake-session';
import { FakeLocks } from '../../testing/fake-locks';

// Les verrous sont accordés de façon asynchrone, comme dans un navigateur.
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('keeps only the access token, in memory, after login', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    const req = httpMock.expectOne('/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });
    expect(service.token()).toBe('AT');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('logout revokes the session through the refresh cookie, with an empty body', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });

    let logoutDone = false;
    service.logout().subscribe(() => (logoutDone = true));

    // Le cookie HttpOnly `rt` part automatiquement — le body est vide.
    const logoutReq = httpMock.expectOne('/api/v1/auth/logout');
    expect(logoutReq.request.method).toBe('POST');
    expect(logoutReq.request.body).toEqual({});
    logoutReq.flush(null, { status: 204, statusText: 'No Content' });

    expect(logoutDone).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('clears the local state even when the server logout fails', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });

    let logoutDone = false;
    service.logout().subscribe(() => (logoutDone = true));

    httpMock
      .expectOne('/api/v1/auth/logout')
      .flush('error', { status: 500, statusText: 'Server Error' });

    expect(logoutDone).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  // Une page distingue « Déconnexion » d'une session expirée (refresh refusé dans l'intercepteur).
  it('tells a logout from a session cleared after a refused refresh', () => {
    const signIn = () => {
      service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
      httpMock
        .expectOne('/api/v1/auth/login')
        .flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });
    };
    signIn();
    expect(service.loggedOut()).toBe(false);

    service.logout().subscribe();
    httpMock
      .expectOne('/api/v1/auth/logout')
      .flush(null, { status: 204, statusText: 'No Content' });
    expect(service.loggedOut()).toBe(true);

    signIn();
    expect(service.loggedOut()).toBe(false);
    service.clearLocalState();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.loggedOut()).toBe(false);
  });

  it('restoreSession() signs in silently with a valid refresh cookie', () => {
    let restored: boolean | undefined;
    service.restoreSession().subscribe((ok) => (restored = ok));

    const req = httpMock.expectOne('/api/v1/auth/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });

    expect(restored).toBe(true);
    expect(service.isAuthenticated()).toBe(true);
  });

  it('restoreSession() returns false without error when there is no refresh cookie (401)', () => {
    let restored: boolean | undefined;
    service.restoreSession().subscribe((ok) => (restored = ok));

    httpMock
      .expectOne('/api/v1/auth/refresh')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(restored).toBe(false);
    expect(service.isAuthenticated()).toBe(false);
  });

  // Les onglets partagent le cookie : un refresh ou un logout attend que l'appel d'un autre onglet
  // ait reçu sa réponse, et donc le cookie qu'elle a posé (voir refresh() et logout()).
  describe('with another tab holding the session cookie lock', () => {
    let locks: FakeLocks;
    let uninstall: () => void;
    let otherTabDone: () => void;

    beforeEach(() => {
      locks = new FakeLocks();
      uninstall = locks.install();
      void locks.request(
        SESSION_COOKIE_LOCK,
        {},
        () => new Promise<void>((resolve) => (otherTabDone = resolve)),
      );
    });
    afterEach(() => uninstall());

    it('sends the refresh once that tab is done, and releases the lock on the answer', async () => {
      service.refresh().subscribe();
      await settle();
      httpMock.expectNone('/api/v1/auth/refresh');

      otherTabDone();
      await settle();
      const req = httpMock.expectOne('/api/v1/auth/refresh');
      expect(locks.held.has(SESSION_COOKIE_LOCK)).toBe(true);
      req.flush({ accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' });
      await settle();

      expect(service.token()).toBe('AT');
      expect(locks.held.has(SESSION_COOKIE_LOCK)).toBe(false);
    });

    it('sends the logout once that tab is done, and releases the lock on the answer', async () => {
      let loggedOut = false;
      service.logout().subscribe(() => (loggedOut = true));
      await settle();
      httpMock.expectNone('/api/v1/auth/logout');

      otherTabDone();
      await settle();
      const req = httpMock.expectOne('/api/v1/auth/logout');
      expect(locks.held.has(SESSION_COOKIE_LOCK)).toBe(true);
      req.flush(null, { status: 204, statusText: 'No Content' });
      await settle();

      expect(loggedOut).toBe(true);
      expect(locks.held.has(SESSION_COOKIE_LOCK)).toBe(false);
    });
  });

  // Le rôle affiché suit le jeton courant : un refresh après un changement de rôle met l'écran à jour.
  it('reads the user, organization and role from the access token, and follows a refresh', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock.expectOne('/api/v1/auth/login').flush({
      accessToken: fakeAccessToken(OWNER_ID, 'Admin'),
      expiresAt: '2030-01-01T00:00:00Z',
    });

    expect(service.session()).toEqual({ userId: OWNER_ID, tenantId: ORG_ID, tenantRole: 'Admin' });
    expect(service.canManageOrganization()).toBe(true);

    service.refresh().subscribe();
    httpMock.expectOne('/api/v1/auth/refresh').flush({
      accessToken: fakeAccessToken(OWNER_ID, 'Member'),
      expiresAt: '2030-01-01T00:00:00Z',
    });

    expect(service.session()?.tenantRole).toBe('Member');
    expect(service.canManageOrganization()).toBe(false);
  });

  it('has no session claims for a token it cannot read', () => {
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: 'not-a-jwt', expiresAt: '2030-01-01T00:00:00Z' });

    expect(service.isAuthenticated()).toBe(true);
    expect(service.session()).toBeNull();
    expect(service.canManageOrganization()).toBe(false);
  });

  // Le payload d'un JWT est en base64url : « - » et « _ » remplacent « + » et « / ».
  it('reads a token payload encoded in base64url', () => {
    const payload = btoa(
      JSON.stringify({ sub: OWNER_ID, tenant: ORG_ID, tenant_role: 'Owner', email: 'a>>>?@b' }),
    );
    expect(payload).toMatch(/[+/]/);
    const token = `h.${payload.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.s`;
    service.login({ email: 'a@b.com', password: 'secret12' }).subscribe();
    httpMock
      .expectOne('/api/v1/auth/login')
      .flush({ accessToken: token, expiresAt: '2030-01-01T00:00:00Z' });

    expect(service.session()?.tenantRole).toBe('Owner');
  });
});
