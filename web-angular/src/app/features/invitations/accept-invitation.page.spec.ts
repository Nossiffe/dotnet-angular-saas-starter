import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { AcceptInvitationPage } from './accept-invitation.page';
import { fieldErrors, showing, touch, typeAndLeave } from '../../testing/form-field';

const ACCEPT_URL = '/api/v1/invitations/accept';
const INVITATION_ID = '0199c9a0-0000-7000-8000-000000000001';
const TOKEN = 'raw-token';
const PASSWORD = 'Un-mot-de-passe-1';

describe('AcceptInvitationPage', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AcceptInvitationPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });
  afterEach(() => httpMock.verify());

  async function open(
    invitationId = INVITATION_ID,
    token = TOKEN,
  ): Promise<ComponentFixture<AcceptInvitationPage>> {
    const fixture = TestBed.createComponent(AcceptInvitationPage);
    fixture.componentRef.setInput('invitationId', invitationId);
    fixture.componentRef.setInput('token', token);
    await fixture.whenStable();
    return fixture;
  }

  function text(fixture: ComponentFixture<AcceptInvitationPage>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function query<T extends Element>(
    fixture: ComponentFixture<AcceptInvitationPage>,
    selector: string,
  ) {
    return (fixture.nativeElement as HTMLElement).querySelector<T>(selector);
  }

  async function answer(
    fixture: ComponentFixture<AcceptInvitationPage>,
    body: object,
    status = 200,
  ): Promise<unknown> {
    const req = httpMock.expectOne(ACCEPT_URL);
    const sent: unknown = req.request.body;
    req.flush(body, { status, statusText: status === 200 ? 'OK' : 'Error' });
    await fixture.whenStable();
    return sent;
  }

  // Erreur métier telle que l'API la renvoie : son code, et la liste des erreurs par code.
  function businessError(code: string, status = 400) {
    return { status, code, errors: { [code]: ['…'] } };
  }

  async function askedForPassword(): Promise<ComponentFixture<AcceptInvitationPage>> {
    const fixture = await open();
    await answer(fixture, businessError('Invitation.PasswordRequired'), 400);
    return fixture;
  }

  async function submitPassword(fixture: ComponentFixture<AcceptInvitationPage>, password: string) {
    const input = query<HTMLInputElement>(fixture, 'input[type=password]')!;
    input.value = password;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    query<HTMLButtonElement>(fixture, 'button[type=submit]')!.click();
    await fixture.whenStable();
  }

  it.each([
    ['no invitation id', '', TOKEN],
    ['an invitation id that is not a GUID', 'not-a-guid', TOKEN],
    ['no token', INVITATION_ID, ''],
  ])(
    'shows the neutral message without calling the API for a link with %s',
    async (_case, invitationId, token) => {
      const fixture = await open(invitationId, token);

      httpMock.expectNone(ACCEPT_URL);
      expect(text(fixture)).toContain('Cette invitation est invalide ou a expiré');
    },
  );

  it('lets an existing account join with a single call, without a password', async () => {
    const fixture = await open();

    const sent = await answer(fixture, { isNewUser: false });

    expect(sent).toEqual({ invitationId: INVITATION_ID, token: TOKEN, password: null });
    expect(text(fixture)).toContain("Vous avez rejoint l'organisation");
    expect(query<HTMLAnchorElement>(fixture, 'a[href="/login"]')).not.toBeNull();
  });

  it('asks for a password when no account exists, then calls again with it', async () => {
    const fixture = await askedForPassword();
    expect(query(fixture, 'input[type=password]')).not.toBeNull();

    await submitPassword(fixture, PASSWORD);
    const sent = await answer(fixture, { isNewUser: true });

    expect(sent).toEqual({ invitationId: INVITATION_ID, token: TOKEN, password: PASSWORD });
    expect(text(fixture)).toContain("Votre compte est créé et vous avez rejoint l'organisation");
  });

  it.each([
    ['an invalid link', 400, businessError('Invitation.Invalid')],
    ['an expired invitation', 400, businessError('Invitation.Expired')],
    ['an invitation already accepted or revoked', 400, businessError('Invitation.NotPending')],
    ['a deleted organization', 404, businessError('Organization.NotFound', 404)],
  ])('shows the same neutral message for %s', async (_case, status, body) => {
    const fixture = await open();

    await answer(fixture, body, status);

    expect(text(fixture)).toContain('Cette invitation est invalide ou a expiré');
    expect(query(fixture, 'input[type=password]')).toBeNull();
  });

  it('keeps the form and points to the rules when the API refuses the password', async () => {
    const fixture = await askedForPassword();
    await submitPassword(fixture, PASSWORD);

    await answer(
      fixture,
      { status: 400, errors: { PasswordRequiresNonAlphanumeric: ['Passwords must have…'] } },
      400,
    );

    expect(text(fixture)).toContain('Ce mot de passe ne respecte pas ces règles.');
    expect(query(fixture, 'input[type=password]')).not.toBeNull();
  });

  it('shows the neutral message when the invitation is revoked before the password call', async () => {
    const fixture = await askedForPassword();
    await submitPassword(fixture, PASSWORD);

    await answer(fixture, businessError('Invitation.NotPending'), 400);

    expect(text(fixture)).toContain('Cette invitation est invalide ou a expiré');
    expect(query(fixture, 'input[type=password]')).toBeNull();
  });

  it('shows the neutral message when the first call fails validation', async () => {
    const fixture = await open();

    await answer(fixture, { status: 400, errors: { Token: ["'Token' must not be empty."] } }, 400);

    expect(text(fixture)).toContain('Cette invitation est invalide ou a expiré');
  });

  it.each([
    ['a server error', 500, { status: 500 }],
    [
      'a validation error unrelated to the password',
      400,
      { status: 400, errors: { DuplicateEmail: ['…'] } },
    ],
  ])(
    'keeps the form with a generic error after %s on the password call',
    async (_case, status, body) => {
      const fixture = await askedForPassword();
      await submitPassword(fixture, PASSWORD);

      await answer(fixture, body, status);

      expect(text(fixture)).toContain('Une erreur est survenue, veuillez réessayer.');
      expect(text(fixture)).not.toContain('Ce mot de passe ne respecte pas ces règles.');
      expect(query(fixture, 'input[type=password]')).not.toBeNull();
    },
  );

  // Les erreurs du mot de passe, sous le champ : la règle de tous les formulaires (FormErrors).
  describe('password errors', () => {
    const password = (fixture: ComponentFixture<AcceptInvitationPage>) =>
      query<HTMLInputElement>(fixture, 'input[type=password]')!;

    it('shows none on the field only entered and left', async () => {
      const fixture = await askedForPassword();

      touch(password(fixture));
      await fixture.whenStable();

      expect(fieldErrors(password(fixture))).toEqual(showing());
    });

    it('shows why a password changed then left is too short, under the field', async () => {
      const fixture = await askedForPassword();

      typeAndLeave(password(fixture), 'trop-court');
      await fixture.whenStable();

      expect(fieldErrors(password(fixture))).toEqual(showing('Au moins 12 caractères'));
    });

    it('does not send a password shorter than 12 characters, and says why', async () => {
      const fixture = await askedForPassword();

      await submitPassword(fixture, 'trop-court');

      httpMock.expectNone(ACCEPT_URL);
      expect(fieldErrors(password(fixture))).toEqual(showing('Au moins 12 caractères'));
    });

    it('does not send an empty password, and says it is required', async () => {
      const fixture = await askedForPassword();

      query<HTMLButtonElement>(fixture, 'button[type=submit]')!.click();
      await fixture.whenStable();

      httpMock.expectNone(ACCEPT_URL);
      expect(fieldErrors(password(fixture))).toEqual(showing('Mot de passe requis'));
    });
  });

  it('offers a retry after a server error', async () => {
    const fixture = await open();
    await answer(fixture, { status: 500 }, 500);
    expect(text(fixture)).toContain('Une erreur est survenue');

    query<HTMLButtonElement>(fixture, 'button[type=button]')!.click();
    await fixture.whenStable();
    await answer(fixture, { isNewUser: false });

    expect(text(fixture)).toContain("Vous avez rejoint l'organisation");
  });
});
