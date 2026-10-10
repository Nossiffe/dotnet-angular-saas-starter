import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { App } from './app';
import { TenantRole } from './core/auth/auth.service';
import { signIn } from './testing/fake-session';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  describe('navigation', () => {
    afterEach(() => TestBed.inject(HttpTestingController).verify());

    const links = async (role?: TenantRole) => {
      if (role) signIn(role);
      const fixture = TestBed.createComponent(App);
      await fixture.whenStable();
      return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('nav a')).map((a) =>
        a.textContent?.trim(),
      );
    };

    it.each<TenantRole>(['Owner', 'Admin'])(
      'shows the Organization link to an %s',
      async (role) => {
        expect(await links(role)).toEqual(['Produits', 'Organisation']);
      },
    );

    it('hides the Organization link from a member', async () => {
      expect(await links('Member')).toEqual(['Produits']);
    });

    it('shows no navigation before login', async () => {
      expect(await links()).toEqual([]);
    });
  });
});
