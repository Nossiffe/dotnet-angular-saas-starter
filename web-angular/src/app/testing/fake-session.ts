import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { AuthService, TenantRole } from '../core/auth/auth.service';

export const ORG_ID = '0199c9a0-0000-7000-8000-0000000000aa';
export const OWNER_ID = '0199c9a0-0000-7000-8000-000000000001';

// Jeton d'accès factice : seul le payload compte pour le front, qui ne vérifie pas la signature.
export function fakeAccessToken(userId: string, role: TenantRole, tenantId = ORG_ID): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return [
    encode({ alg: 'HS256', typ: 'JWT' }),
    encode({ sub: userId, tenant: tenantId, tenant_role: role, email: 'me@saas.local' }),
    'signature',
  ].join('.');
}

/** Ouvre une session par une connexion simulée : le jeton reçu porte ce rôle. */
export function signIn(role: TenantRole, userId = OWNER_ID): void {
  TestBed.inject(AuthService)
    .login({ email: 'me@saas.local', password: 'secret-password' })
    .subscribe();
  TestBed.inject(HttpTestingController)
    .expectOne('/api/v1/auth/login')
    .flush({ accessToken: fakeAccessToken(userId, role), expiresAt: '2030-01-01T00:00:00Z' });
}
