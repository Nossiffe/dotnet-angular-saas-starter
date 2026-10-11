import { Component, type Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { LoginPage } from './login.page';
import { RegisterPage } from './register.page';
import { ForgotPasswordPage } from './forgot-password.page';
import { ResetPasswordPage } from './reset-password.page';
import { fieldErrors, showing, touch, typeAndLeave } from '../../testing/form-field';

// La page vers laquelle un formulaire envoyé mène (connexion, produits).
@Component({ template: '' })
class Elsewhere {}

interface Field {
  label: string;
  /** A value the field refuses, the message that says why, and the message of an empty field. */
  invalid: string;
  message: string;
  required: string;
  /** A value the field accepts. */
  valid: string;
}

const EMAIL: Field = {
  label: 'Email',
  invalid: 'pas-un-email',
  message: 'Email invalide',
  required: 'Email requis',
  valid: 'marie@saas.local',
};
const password = (label: string, minLength: number): Field => ({
  label,
  invalid: 'court',
  message: `Au moins ${minLength} caractères`,
  required: 'Mot de passe requis',
  valid: 'Un-mot-de-passe-1',
});

interface AuthForm {
  name: string;
  page: Type<unknown>;
  inputs?: Record<string, string>;
  url: string;
  answer: object | null;
  fields: Field[];
}

const FORMS: AuthForm[] = [
  {
    name: 'login',
    page: LoginPage,
    url: '/api/v1/auth/login',
    answer: { accessToken: 'AT', expiresAt: '2030-01-01T00:00:00Z' },
    fields: [EMAIL, password('Mot de passe', 8)],
  },
  {
    name: 'registration',
    page: RegisterPage,
    url: '/api/v1/auth/register',
    answer: null,
    fields: [EMAIL, password('Mot de passe', 12)],
  },
  {
    name: 'forgotten password',
    page: ForgotPasswordPage,
    url: '/api/v1/auth/forgot-password',
    answer: null,
    fields: [EMAIL],
  },
  {
    name: 'password reset',
    page: ResetPasswordPage,
    inputs: { userId: '0199c9a0-0000-7000-8000-000000000001', token: 'raw-token' },
    url: '/api/v1/auth/reset-password',
    answer: null,
    fields: [password('Nouveau mot de passe', 12)],
  },
];

describe.each(FORMS)('the $name form', (authForm) => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [authForm.page],
      providers: [
        provideRouter([{ path: '**', component: Elsewhere }]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  async function open(): Promise<ComponentFixture<unknown>> {
    const fixture = TestBed.createComponent(authForm.page);
    for (const [name, value] of Object.entries(authForm.inputs ?? {})) {
      fixture.componentRef.setInput(name, value);
    }
    await fixture.whenStable();
    return fixture;
  }

  function input(fixture: ComponentFixture<unknown>, label: string): HTMLInputElement {
    const field = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('mat-form-field'),
    ).find((f) => f.querySelector('mat-label')?.textContent?.trim() === label);
    const element = field?.querySelector('input');
    if (!element) throw new Error(`No field labelled « ${label} ».`);
    return element;
  }

  async function submit(fixture: ComponentFixture<unknown>): Promise<void> {
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('button[type=submit]')!
      .click();
    await fixture.whenStable();
  }

  it('shows no error on a field only entered and left', async () => {
    const fixture = await open();

    for (const field of authForm.fields) touch(input(fixture, field.label));
    await fixture.whenStable();

    for (const field of authForm.fields) {
      expect(fieldErrors(input(fixture, field.label))).toEqual(showing());
    }
  });

  it('shows no error while the user is still typing in the field', async () => {
    const fixture = await open();
    const first = input(fixture, authForm.fields[0]!.label);

    first.dispatchEvent(new Event('focus'));
    first.value = authForm.fields[0]!.invalid;
    first.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(fieldErrors(first)).toEqual(showing());
  });

  it('shows why a field changed then left is refused, under that field only', async () => {
    const fixture = await open();
    const [first, ...others] = authForm.fields;

    typeAndLeave(input(fixture, first!.label), first!.invalid);
    await fixture.whenStable();

    expect(fieldErrors(input(fixture, first!.label))).toEqual(showing(first!.message));
    for (const field of others) {
      expect(fieldErrors(input(fixture, field.label))).toEqual(showing());
    }
  });

  it('sends nothing when submitted empty, and shows why under every field', async () => {
    const fixture = await open();

    await submit(fixture);

    httpMock.expectNone(authForm.url);
    for (const field of authForm.fields) {
      expect(fieldErrors(input(fixture, field.label))).toEqual(showing(field.required));
    }
  });

  it('sends the form once every field is valid, its errors gone', async () => {
    const fixture = await open();
    await submit(fixture);

    for (const field of authForm.fields) typeAndLeave(input(fixture, field.label), field.valid);
    await fixture.whenStable();
    for (const field of authForm.fields) {
      expect(fieldErrors(input(fixture, field.label))).toEqual(showing());
    }
    await submit(fixture);

    httpMock.expectOne(authForm.url).flush(authForm.answer);
    await fixture.whenStable();
  });
});
