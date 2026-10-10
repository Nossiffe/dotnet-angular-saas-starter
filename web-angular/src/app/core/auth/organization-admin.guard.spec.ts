import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { signIn } from '../../testing/fake-session';
import { TenantRole } from './auth.service';
import { organizationAdminGuard } from './organization-admin.guard';

describe('organizationAdminGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const run = () =>
    TestBed.runInInjectionContext(() =>
      organizationAdminGuard(
        {} as Parameters<typeof organizationAdminGuard>[0],
        {} as Parameters<typeof organizationAdminGuard>[1],
      ),
    );

  it.each<TenantRole>(['Owner', 'Admin'])('lets an %s in', (role) => {
    signIn(role);

    expect(run()).toBe(true);
  });

  it('sends a member to the products', () => {
    signIn('Member');

    const result = run();

    expect(result).toBeInstanceOf(UrlTree);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/products');
  });
});
