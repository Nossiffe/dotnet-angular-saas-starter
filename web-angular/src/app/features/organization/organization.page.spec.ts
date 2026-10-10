import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { InvitationDto, MemberDto } from '../../api/generated';
import { authInterceptor } from '../../core/auth/auth.interceptor';
import { AuthService } from '../../core/auth/auth.service';
import { httpErrorInterceptor } from '../../core/error/http-error.interceptor';
import { fakeAccessToken, ORG_ID, OWNER_ID, signIn } from '../../testing/fake-session';
import { OrganizationPage } from './organization.page';

const MEMBERS_URL = `/api/v1/organizations/${ORG_ID}/members`;
const INVITATIONS_URL = `/api/v1/organizations/${ORG_ID}/invitations`;
const ADMIN_ID = '0199c9a0-0000-7000-8000-000000000002';
const MEMBER_ID = '0199c9a0-0000-7000-8000-000000000003';
const GONE_ID = '0199c9a0-0000-7000-8000-000000000009';

function member(userId: string, email: string, role: string, day: string): MemberDto {
  return { userId, email, role, joinedAt: `2026-10-${day}T09:00:00Z` };
}

const MEMBERS: MemberDto[] = [
  member(OWNER_ID, 'owner@saas.local', 'Owner', '01'),
  member(ADMIN_ID, 'admin@saas.local', 'Admin', '02'),
  member(MEMBER_ID, 'member@saas.local', 'Member', '03'),
];

function invitation(id: string, email: string, invitedByUserId: string, role = 'Member') {
  return {
    id,
    email,
    role,
    invitedByUserId,
    expiresAt: '2026-10-17T09:00:00Z',
    createdAt: '2026-10-10T09:00:00Z',
  } satisfies InvitationDto;
}

const BY_OWNER = invitation('0199c9a0-0000-7000-8000-0000000000b1', 'new@saas.local', OWNER_ID);
const BY_ADMIN = invitation(
  '0199c9a0-0000-7000-8000-0000000000b2',
  'other-me@saas.local',
  ADMIN_ID,
  'Admin',
);
const BY_FORMER = invitation('0199c9a0-0000-7000-8000-0000000000b3', 'x@saas.local', GONE_ID);
const INVITATIONS = [BY_OWNER, BY_ADMIN, BY_FORMER];

const NO_CONTENT = { status: 204, statusText: 'No Content' };

function problem(status: number, code?: string, errors?: Record<string, string[]>) {
  return {
    body: { status, title: 'refus', ...(code ? { code } : {}), ...(errors ? { errors } : {}) },
    options: { status, statusText: 'Error' },
  };
}

describe('OrganizationPage', () => {
  let httpMock: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [OrganizationPage],
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor, httpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
    httpMock.verify();
  });

  async function open(
    role: 'Owner' | 'Admin' = 'Owner',
    userId = role === 'Owner' ? OWNER_ID : ADMIN_ID,
    members = MEMBERS,
    invitations: InvitationDto[] = INVITATIONS,
  ): Promise<ComponentFixture<OrganizationPage>> {
    signIn(role, userId);
    const fixture = TestBed.createComponent(OrganizationPage);
    TestBed.tick();
    httpMock.expectOne({ method: 'GET', url: MEMBERS_URL }).flush(members);
    httpMock.expectOne({ method: 'GET', url: INVITATIONS_URL }).flush(invitations);
    await fixture.whenStable();
    return fixture;
  }

  // Après une action, la page relit la liste concernée.
  async function reload(
    fixture: ComponentFixture<OrganizationPage>,
    url: string,
    body: MemberDto[] | InvitationDto[],
  ) {
    TestBed.tick();
    httpMock.expectOne({ method: 'GET', url }).flush(body);
    await fixture.whenStable();
  }

  const page = (fixture: ComponentFixture<OrganizationPage>) =>
    fixture.nativeElement as HTMLElement;
  const text = (fixture: ComponentFixture<OrganizationPage>) => page(fixture).textContent ?? '';
  const status = (fixture: ComponentFixture<OrganizationPage>) =>
    page(fixture).querySelector('[role=status]')?.textContent?.trim();
  const alerts = (fixture: ComponentFixture<OrganizationPage>) =>
    Array.from(page(fixture).querySelectorAll('[role=alert]')).map((a) => a.textContent?.trim());
  // Cellules de chaque ligne d'un tableau (0 : membres, 1 : invitations), sans la colonne d'actions.
  const rows = (fixture: ComponentFixture<OrganizationPage>, table: number) =>
    Array.from(
      page(fixture).querySelectorAll('table')[table]!.querySelectorAll('tr.mat-mdc-row'),
    ).map((row) =>
      Array.from(row.querySelectorAll('td'))
        .filter((cell) => !cell.classList.contains('mat-column-actions'))
        .map((cell) => {
          const select = cell.querySelector('select');
          return select ? select.value : (cell.textContent?.trim() ?? '');
        }),
    );
  const button = (fixture: ComponentFixture<OrganizationPage>, label: string) =>
    page(fixture).querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
  const roleSelect = (fixture: ComponentFixture<OrganizationPage>, email: string) =>
    page(fixture).querySelector<HTMLSelectElement>(`select[aria-label="Rôle de ${email}"]`);
  const heading = (fixture: ComponentFixture<OrganizationPage>, id: string) =>
    page(fixture).querySelector(`#${id}`);
  const snackBar = () => document.querySelector('mat-snack-bar-container');
  // Laisse passer les tâches en attente (une erreur non gérée remonterait ici à Vitest).
  const nextTask = () => new Promise((resolve) => setTimeout(resolve, 0));
  // Laisse passer les réponses déjà fournies, sans attendre les requêtes encore ouvertes.
  async function settle(fixture: ComponentFixture<OrganizationPage>) {
    await nextTask();
    fixture.detectChanges();
  }

  describe('display', () => {
    it('lists the members with their e-mail, role and arrival date', async () => {
      const fixture = await open();

      expect(rows(fixture, 0)).toEqual([
        ['owner@saas.local', 'Owner', '01/10/2026'],
        ['admin@saas.local', 'Admin', '02/10/2026'],
        ['member@saas.local', 'Member', '03/10/2026'],
      ]);
    });

    // L'Owner change le rôle et retire les autres membres, jamais un Owner ni lui-même.
    it('gives the owner role and removal controls for the other non-owner members only', async () => {
      const fixture = await open('Owner');

      expect(roleSelect(fixture, 'admin@saas.local')?.value).toBe('Admin');
      expect(roleSelect(fixture, 'member@saas.local')?.value).toBe('Member');
      expect(roleSelect(fixture, 'owner@saas.local')).toBeNull();
      expect(button(fixture, 'Retirer admin@saas.local')).not.toBeNull();
      expect(button(fixture, 'Retirer member@saas.local')).not.toBeNull();
      expect(button(fixture, 'Retirer owner@saas.local')).toBeNull();
    });

    // Un autre Owner (possible en base) n'a ni changement de rôle ni retrait.
    it('gives no control on another owner', async () => {
      const coOwner = member(GONE_ID, 'co-owner@saas.local', 'Owner', '04');
      const fixture = await open('Owner', OWNER_ID, [...MEMBERS, coOwner]);

      expect(roleSelect(fixture, 'co-owner@saas.local')).toBeNull();
      expect(button(fixture, 'Retirer co-owner@saas.local')).toBeNull();
    });

    // Jeton d'Owner, mais la liste montre l'utilisateur avec un autre rôle : rien sur lui-même.
    it('gives the user no control on themselves, whatever role the list shows', async () => {
      const self = member(OWNER_ID, 'owner@saas.local', 'Admin', '01');
      const fixture = await open('Owner', OWNER_ID, [self, ...MEMBERS.slice(1)]);

      expect(roleSelect(fixture, 'owner@saas.local')).toBeNull();
      expect(button(fixture, 'Retirer owner@saas.local')).toBeNull();
    });

    it('shows the members to an admin without any role change or removal', async () => {
      const fixture = await open('Admin');

      expect(text(fixture)).toContain('member@saas.local');
      expect(page(fixture).querySelectorAll('select[aria-label^="Rôle de"]')).toHaveLength(0);
      expect(button(fixture, 'Retirer member@saas.local')).toBeNull();
      expect(button(fixture, `Révoquer l'invitation de new@saas.local`)).not.toBeNull();
    });

    it('lists the pending invitations with who sent them', async () => {
      const fixture = await open('Owner');

      expect(rows(fixture, 1)).toEqual([
        ['new@saas.local', 'Member', 'Vous', '17/10/2026'],
        ['other-me@saas.local', 'Admin', 'admin@saas.local', '17/10/2026'],
        ['x@saas.local', 'Member', 'Ancien membre', '17/10/2026'],
      ]);
    });

    it.each([
      [500, undefined, 'Une erreur est survenue, réessayez.'],
      [403, undefined, 'Votre rôle ne permet pas cette action.'],
      [
        404,
        'Organization.NotFound',
        "Cette organisation n'est plus accessible avec votre session.",
      ],
    ])(
      'shows a members load error %s %s by its code, without a toast',
      async (code, name, message) => {
        signIn('Owner');
        const fixture = TestBed.createComponent(OrganizationPage);
        TestBed.tick();
        const failure = problem(code, name);
        httpMock
          .expectOne({ method: 'GET', url: MEMBERS_URL })
          .flush(failure.body, failure.options);
        httpMock.expectOne({ method: 'GET', url: INVITATIONS_URL }).flush(INVITATIONS);
        await fixture.whenStable();

        expect(alerts(fixture)).toContain(message);
        expect(snackBar()).toBeNull();
        // Sans la liste des membres, l'expéditeur d'une invitation reste inconnu, sauf « Vous ».
        expect(rows(fixture, 0).map((cells) => cells[2])).toEqual(['Vous', '—', '—']);
      },
    );

    it('shows an invitations load error by its code, without a toast', async () => {
      signIn('Owner');
      const fixture = TestBed.createComponent(OrganizationPage);
      TestBed.tick();
      httpMock.expectOne({ method: 'GET', url: MEMBERS_URL }).flush(MEMBERS);
      const failure = problem(500);
      httpMock
        .expectOne({ method: 'GET', url: INVITATIONS_URL })
        .flush(failure.body, failure.options);
      await fixture.whenStable();

      expect(alerts(fixture)).toEqual(['Une erreur est survenue, réessayez.']);
      expect(snackBar()).toBeNull();
      expect(rows(fixture, 0)).toHaveLength(3);
    });
  });

  describe('role change', () => {
    async function choose(
      fixture: ComponentFixture<OrganizationPage>,
      email: string,
      role: string,
    ) {
      const select = roleSelect(fixture, email)!;
      select.value = role;
      select.dispatchEvent(new Event('change'));
      await fixture.whenStable();
    }

    // Choisir un rôle (à la souris ou d'une flèche du clavier) n'envoie rien : seul « Appliquer »
    // envoie le changement.
    it('sends nothing until the change is applied', async () => {
      const fixture = await open();

      expect(button(fixture, 'Appliquer le rôle de member@saas.local')!.disabled).toBe(true);
      await choose(fixture, 'member@saas.local', 'Admin');

      httpMock.expectNone({ method: 'PUT' });
      expect(button(fixture, 'Appliquer le rôle de member@saas.local')!.disabled).toBe(false);
    });

    it('sends the new role, then reloads the members and focuses their heading', async () => {
      const fixture = await open();
      await choose(fixture, 'member@saas.local', 'Admin');

      button(fixture, 'Appliquer le rôle de member@saas.local')!.click();
      await settle(fixture);
      // Pendant l'envoi, les autres actions sur les membres attendent.
      expect(button(fixture, 'Retirer admin@saas.local')!.disabled).toBe(true);
      expect(roleSelect(fixture, 'admin@saas.local')!.disabled).toBe(true);
      const put = httpMock.expectOne({ method: 'PUT', url: `${MEMBERS_URL}/${MEMBER_ID}/role` });
      expect(put.request.body).toEqual({ role: 'Admin' });
      put.flush(null, NO_CONTENT);
      await reload(fixture, MEMBERS_URL, MEMBERS);
      expect(button(fixture, 'Retirer admin@saas.local')!.disabled).toBe(false);

      expect(status(fixture)).toBe('Rôle de member@saas.local : Admin.');
      expect(document.activeElement).toBe(heading(fixture, 'members-title'));
    });

    it('shows the accepted role while the members are read again', async () => {
      const fixture = await open();
      await choose(fixture, 'member@saas.local', 'Admin');

      button(fixture, 'Appliquer le rôle de member@saas.local')!.click();
      httpMock
        .expectOne({ method: 'PUT', url: `${MEMBERS_URL}/${MEMBER_ID}/role` })
        .flush(null, NO_CONTENT);
      await settle(fixture);

      expect(roleSelect(fixture, 'member@saas.local')!.value).toBe('Admin');
      expect(button(fixture, 'Appliquer le rôle de member@saas.local')!.disabled).toBe(true);
      const promoted = MEMBERS.map((m) => (m.userId === MEMBER_ID ? { ...m, role: 'Admin' } : m));
      await reload(fixture, MEMBERS_URL, promoted);
      expect(roleSelect(fixture, 'member@saas.local')!.value).toBe('Admin');
    });

    // Page quittée pendant l'envoi : le changement va à son terme, sans relecture ni erreur.
    it('ends quietly a role change applied just before the page is left', async () => {
      const fixture = await open();
      await choose(fixture, 'member@saas.local', 'Admin');
      button(fixture, 'Appliquer le rôle de member@saas.local')!.click();

      fixture.destroy();
      httpMock
        .expectOne({ method: 'PUT', url: `${MEMBERS_URL}/${MEMBER_ID}/role` })
        .flush(null, NO_CONTENT);
      await nextTask();

      httpMock.expectNone({ method: 'GET', url: MEMBERS_URL });
    });

    it.each([
      [400, 'Membership.CannotChangeOwnRole', 'Vous ne pouvez pas modifier votre propre rôle.'],
      [
        400,
        'Organization.LastOwner',
        "Impossible de rétrograder le dernier Owner de l'organisation.",
      ],
      [400, 'Membership.InvalidRole', 'Ce rôle ne peut pas être attribué.'],
      [404, 'Membership.NotFound', "Ce membre ne fait plus partie de l'organisation."],
      [403, undefined, 'Votre rôle ne permet pas cette action.'],
      [409, undefined, 'Cette ressource a été modifiée ailleurs, réessayez.'],
    ])(
      'shows the refusal %s %s, reloads the members and shows their role again',
      async (code, name, message) => {
        const fixture = await open();
        await choose(fixture, 'admin@saas.local', 'Member');

        button(fixture, 'Appliquer le rôle de admin@saas.local')!.click();
        const failure = problem(code, name);
        httpMock
          .expectOne({ method: 'PUT', url: `${MEMBERS_URL}/${ADMIN_ID}/role` })
          .flush(failure.body, failure.options);
        await reload(fixture, MEMBERS_URL, MEMBERS);

        expect(alerts(fixture)).toContain(message);
        expect(roleSelect(fixture, 'admin@saas.local')?.value).toBe('Admin');
        expect(snackBar()).toBeNull();
      },
    );
  });

  describe('removal', () => {
    const dialog = () => document.querySelector<HTMLElement>('mat-dialog-container');
    const dialogButton = (label: string) =>
      Array.from(dialog()?.querySelectorAll('button') ?? []).find(
        (b) => b.textContent?.trim() === label,
      )!;

    // La confirmation relit les invitations en attente avant de s'ouvrir.
    async function askToRemove(
      fixture: ComponentFixture<OrganizationPage>,
      email: string,
      pending: InvitationDto[] | 'fails' = INVITATIONS,
    ) {
      button(fixture, `Retirer ${email}`)!.click();
      const read = httpMock.expectOne({ method: 'GET', url: INVITATIONS_URL });
      if (pending === 'fails') {
        const failure = problem(500);
        read.flush(failure.body, failure.options);
      } else {
        read.flush(pending);
      }
      await fixture.whenStable();
    }

    async function confirm(fixture: ComponentFixture<OrganizationPage>) {
      dialogButton('Retirer').click();
      await fixture.whenStable();
    }

    async function cancel(fixture: ComponentFixture<OrganizationPage>) {
      dialogButton('Annuler').click();
      await fixture.whenStable();
    }

    function removalSucceeds() {
      httpMock
        .expectOne({ method: 'DELETE', url: `${MEMBERS_URL}/${ADMIN_ID}` })
        .flush(null, NO_CONTENT);
    }

    async function listsReloaded(
      fixture: ComponentFixture<OrganizationPage>,
      invitations: InvitationDto[],
    ) {
      TestBed.tick();
      httpMock.expectOne({ method: 'GET', url: INVITATIONS_URL }).flush(invitations);
      httpMock.expectOne({ method: 'GET', url: MEMBERS_URL }).flush(MEMBERS);
      await fixture.whenStable();
    }

    it('lists the pending invitations the member sent, to revoke them too by default', async () => {
      const fixture = await open();

      await askToRemove(fixture, 'admin@saas.local');

      expect(dialog()?.textContent).toContain('other-me@saas.local (Admin)');
      expect(dialog()?.textContent).not.toContain('new@saas.local');
      expect(dialog()?.querySelector<HTMLInputElement>('input[type=checkbox]')?.checked).toBe(true);
      await cancel(fixture);
    });

    // La liste affichée n'est pas encore lue : la confirmation relit les invitations elle-même.
    it('reads the invitations itself when the list is still loading', async () => {
      signIn('Owner');
      const fixture = TestBed.createComponent(OrganizationPage);
      TestBed.tick();
      httpMock.expectOne({ method: 'GET', url: MEMBERS_URL }).flush(MEMBERS);
      // whenStable attendrait la liste des invitations, laissée en cours de chargement.
      await settle(fixture);

      button(fixture, 'Retirer admin@saas.local')!.click();
      const [initial, fresh] = httpMock.match({ method: 'GET', url: INVITATIONS_URL });
      fresh!.flush(INVITATIONS);
      await settle(fixture);

      expect(dialog()?.textContent).toContain('other-me@saas.local (Admin)');
      initial!.flush(INVITATIONS);
      await cancel(fixture);
    });

    it('says when the invitations could not be read, and still removes the member', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local', 'fails');

      expect(dialog()?.textContent).toContain("Les invitations en attente n'ont pas pu être lues");
      expect(dialog()?.querySelector('input[type=checkbox]')).toBeNull();
      await confirm(fixture);
      removalSucceeds();
      await listsReloaded(fixture, INVITATIONS);

      httpMock.expectNone({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` });
      expect(status(fixture)).toBe("admin@saas.local a été retiré de l'organisation.");
    });

    it('removes the member, then revokes the invitations they sent', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local');

      await confirm(fixture);
      removalSucceeds();
      httpMock
        .expectOne({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` })
        .flush(null, NO_CONTENT);
      await listsReloaded(fixture, [BY_OWNER, BY_FORMER]);

      expect(status(fixture)).toBe(
        "admin@saas.local a été retiré de l'organisation. Invitations révoquées : 1.",
      );
      expect(document.activeElement).toBe(heading(fixture, 'members-title'));
    });

    it('keeps the invitations when the box is unchecked', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local');
      dialog()!.querySelector<HTMLInputElement>('input[type=checkbox]')!.click();
      await fixture.whenStable();

      await confirm(fixture);
      removalSucceeds();
      await listsReloaded(fixture, INVITATIONS);

      httpMock.expectNone({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` });
      expect(status(fixture)).toBe("admin@saas.local a été retiré de l'organisation.");
    });

    it('asks no question about invitations for a member who sent none', async () => {
      const fixture = await open();

      await askToRemove(fixture, 'member@saas.local');

      expect(dialog()?.querySelector('input[type=checkbox]')).toBeNull();
      expect(dialog()?.textContent).toContain(
        "member@saas.local perdra immédiatement l'accès à l'organisation.",
      );
      await cancel(fixture);
    });

    // Page quittée pendant la relecture des invitations : aucune confirmation ne s'ouvre ailleurs.
    it('opens no confirmation once the page is left', async () => {
      const fixture = await open();
      button(fixture, 'Retirer admin@saas.local')!.click();
      const read = httpMock.expectOne({ method: 'GET', url: INVITATIONS_URL });

      fixture.destroy();
      await nextTask();

      expect(read.cancelled).toBe(true);
      expect(dialog()).toBeNull();
    });

    // Confirmé, puis page quittée : le retrait et la révocation choisie vont à leur terme.
    it('still revokes the chosen invitations when the page is left after the confirmation', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local');
      await confirm(fixture);

      fixture.destroy();
      removalSucceeds();
      httpMock
        .expectOne({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` })
        .flush(null, NO_CONTENT);
      await nextTask();

      httpMock.expectNone({ method: 'GET' });
    });

    it('does nothing when the removal is cancelled', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local');

      await cancel(fixture);

      httpMock.expectNone({ method: 'DELETE' });
      expect(dialog()).toBeNull();
    });

    it.each([
      [400, 'Membership.CannotRemoveOwner', "Un Owner ne peut pas être retiré de l'organisation."],
      [403, undefined, 'Votre rôle ne permet pas cette action.'],
      [409, undefined, 'Cette ressource a été modifiée ailleurs, réessayez.'],
    ])(
      'shows the refusal %s %s, revokes nothing and reloads the members',
      async (code, name, message) => {
        const fixture = await open();
        await askToRemove(fixture, 'admin@saas.local');

        await confirm(fixture);
        const failure = problem(code, name);
        httpMock
          .expectOne({ method: 'DELETE', url: `${MEMBERS_URL}/${ADMIN_ID}` })
          .flush(failure.body, failure.options);
        await reload(fixture, MEMBERS_URL, MEMBERS);

        httpMock.expectNone({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` });
        expect(alerts(fixture)).toContain(message);
      },
    );

    // Déjà parti : ses invitations sont tout de même révoquées, comme demandé.
    it('still revokes the invitations of a member who had already left', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local');

      await confirm(fixture);
      const gone = problem(404, 'Membership.NotFound');
      httpMock
        .expectOne({ method: 'DELETE', url: `${MEMBERS_URL}/${ADMIN_ID}` })
        .flush(gone.body, gone.options);
      httpMock
        .expectOne({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` })
        .flush(null, NO_CONTENT);
      await listsReloaded(fixture, [BY_OWNER, BY_FORMER]);

      expect(status(fixture)).toBe(
        "Ce membre ne fait plus partie de l'organisation. Invitations révoquées : 1.",
      );
    });

    it('names each invitation that could not be revoked, with the reason', async () => {
      const fixture = await open();
      await askToRemove(fixture, 'admin@saas.local');

      await confirm(fixture);
      removalSucceeds();
      const accepted = problem(400, 'Invitation.NotPending');
      httpMock
        .expectOne({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_ADMIN.id}` })
        .flush(accepted.body, accepted.options);
      await listsReloaded(fixture, INVITATIONS);

      expect(status(fixture)).toBe("admin@saas.local a été retiré de l'organisation.");
      expect(alerts(fixture)).toContain(
        "Invitations non révoquées : other-me@saas.local (Cette invitation n'est plus en attente.). " +
          'Vérifiez les listes des membres et des invitations.',
      );
    });
  });

  describe('invitations', () => {
    const inviteForm = (fixture: ComponentFixture<OrganizationPage>) =>
      page(fixture).querySelector<HTMLFormElement>('form.invite')!;

    async function fillInvite(
      fixture: ComponentFixture<OrganizationPage>,
      email: string,
      role: 'Member' | 'Admin',
    ) {
      const input = inviteForm(fixture).querySelector<HTMLInputElement>('input[type=email]')!;
      const select = inviteForm(fixture).querySelector<HTMLSelectElement>('select')!;
      input.value = email;
      input.dispatchEvent(new Event('input'));
      select.value = role;
      select.dispatchEvent(new Event('input'));
      select.dispatchEvent(new Event('change'));
      inviteForm(fixture).dispatchEvent(new Event('submit'));
      await fixture.whenStable();
    }

    it('sends an invitation, then reloads the list, empties the form and focuses it', async () => {
      const fixture = await open();

      await fillInvite(fixture, 'colleague@saas.local', 'Admin');
      const post = httpMock.expectOne({ method: 'POST', url: INVITATIONS_URL });
      expect(post.request.body).toEqual({ email: 'colleague@saas.local', role: 'Admin' });
      post.flush(
        { invitationId: '0199c9a0-0000-7000-8000-0000000000c1' },
        { status: 201, statusText: 'Created' },
      );
      await reload(fixture, INVITATIONS_URL, INVITATIONS);

      const input = inviteForm(fixture).querySelector<HTMLInputElement>('input')!;
      expect(status(fixture)).toBe('Invitation envoyée à colleague@saas.local.');
      expect(input.value).toBe('');
      expect(document.activeElement).toBe(input);
    });

    // Un second envoi pendant le premier ne part pas.
    it('sends a single invitation when the form is submitted twice', async () => {
      const fixture = await open();

      await fillInvite(fixture, 'colleague@saas.local', 'Member');
      inviteForm(fixture).dispatchEvent(new Event('submit'));
      await settle(fixture);

      const posts = httpMock.match({ method: 'POST', url: INVITATIONS_URL });
      expect(posts).toHaveLength(1);
      posts[0]!.flush(
        { invitationId: '0199c9a0-0000-7000-8000-0000000000c1' },
        { status: 201, statusText: 'Created' },
      );
      await reload(fixture, INVITATIONS_URL, INVITATIONS);
    });

    it('does not send an invalid e-mail', async () => {
      const fixture = await open();

      await fillInvite(fixture, 'not-an-email', 'Member');

      httpMock.expectNone({ method: 'POST', url: INVITATIONS_URL });
      expect(text(fixture)).toContain('Adresse e-mail invalide');
    });

    it.each([
      [
        409,
        'Invitation.AlreadyPending',
        undefined,
        'Une invitation est déjà en attente pour cette adresse.',
      ],
      // Sans code : l'index unique des invitations en attente (course entre deux envois).
      [409, undefined, undefined, 'Une invitation est déjà en attente pour cette adresse.'],
      [
        400,
        'Membership.AlreadyMember',
        undefined,
        "Cette personne est déjà membre de l'organisation.",
      ],
      [
        400,
        undefined,
        { Email: ["'Email' is not a valid email address."] },
        'Adresse e-mail invalide.',
      ],
    ])(
      'shows the refusal %s %s by its code, without a toast',
      async (code, name, errors, message) => {
        const fixture = await open();

        await fillInvite(fixture, 'someone@saas.local', 'Member');
        const failure = problem(code, name, errors);
        httpMock
          .expectOne({ method: 'POST', url: INVITATIONS_URL })
          .flush(failure.body, failure.options);
        await reload(fixture, INVITATIONS_URL, INVITATIONS);

        expect(alerts(fixture)).toContain(message);
        expect(snackBar()).toBeNull();
      },
    );

    it('clears an invitation error when the next action starts', async () => {
      const fixture = await open();
      await fillInvite(fixture, 'someone@saas.local', 'Member');
      const failure = problem(400, 'Membership.AlreadyMember');
      httpMock
        .expectOne({ method: 'POST', url: INVITATIONS_URL })
        .flush(failure.body, failure.options);
      await reload(fixture, INVITATIONS_URL, INVITATIONS);

      button(fixture, `Renvoyer l'invitation à new@saas.local`)!.click();
      await settle(fixture);

      expect(alerts(fixture)).toEqual([]);
      httpMock
        .expectOne({ method: 'POST', url: `${INVITATIONS_URL}/${BY_OWNER.id}/resend` })
        .flush(null, NO_CONTENT);
      await reload(fixture, INVITATIONS_URL, INVITATIONS);
    });

    it('resends an invitation, locking the other actions until the list is read again', async () => {
      const fixture = await open();

      button(fixture, `Renvoyer l'invitation à new@saas.local`)!.click();
      await settle(fixture);
      expect(button(fixture, `Révoquer l'invitation de x@saas.local`)!.disabled).toBe(true);
      httpMock
        .expectOne({ method: 'POST', url: `${INVITATIONS_URL}/${BY_OWNER.id}/resend` })
        .flush(null, NO_CONTENT);
      await settle(fixture);
      expect(button(fixture, `Révoquer l'invitation de x@saas.local`)!.disabled).toBe(true);
      await reload(fixture, INVITATIONS_URL, INVITATIONS);

      expect(status(fixture)).toBe('Invitation renvoyée à new@saas.local.');
      expect(button(fixture, `Révoquer l'invitation de x@saas.local`)!.disabled).toBe(false);
      expect(document.activeElement).toBe(heading(fixture, 'invitations-title'));
    });

    it('revokes an invitation', async () => {
      const fixture = await open();

      button(fixture, `Révoquer l'invitation de new@saas.local`)!.click();
      httpMock
        .expectOne({ method: 'DELETE', url: `${INVITATIONS_URL}/${BY_OWNER.id}` })
        .flush(null, NO_CONTENT);
      await reload(fixture, INVITATIONS_URL, [BY_ADMIN, BY_FORMER]);

      expect(status(fixture)).toBe('Invitation pour new@saas.local révoquée.');
      expect(rows(fixture, 1).map((cells) => cells[0])).toEqual([
        'other-me@saas.local',
        'x@saas.local',
      ]);
    });

    it.each([
      [
        'Renvoyer',
        'POST',
        '/resend',
        400,
        'Invitation.NotPending',
        "Cette invitation n'est plus en attente.",
      ],
      [
        'Renvoyer',
        'POST',
        '/resend',
        404,
        'Invitation.NotFound',
        "Cette invitation n'existe plus.",
      ],
      [
        'Révoquer',
        'DELETE',
        '',
        400,
        'Invitation.NotPending',
        "Cette invitation n'est plus en attente.",
      ],
      ['Révoquer', 'DELETE', '', 404, 'Invitation.NotFound', "Cette invitation n'existe plus."],
    ])(
      'shows the refusal of « %s » %s %s by its code and reloads the list',
      async (action, method, suffix, code, name, message) => {
        const fixture = await open();
        const label =
          action === 'Renvoyer'
            ? `Renvoyer l'invitation à new@saas.local`
            : `Révoquer l'invitation de new@saas.local`;

        button(fixture, label)!.click();
        const failure = problem(code, name);
        httpMock
          .expectOne({ method, url: `${INVITATIONS_URL}/${BY_OWNER.id}${suffix}` })
          .flush(failure.body, failure.options);
        await reload(fixture, INVITATIONS_URL, INVITATIONS);

        expect(alerts(fixture)).toContain(message);
        expect(snackBar()).toBeNull();
      },
    );
  });

  // Un 401 Auth.SessionExpired (jeton périmé : rôle ou adhésion changés) passe par le refresh de
  // authInterceptor, qui rejoue la requête une fois avec le nouveau jeton.
  describe('stale session', () => {
    const expired = problem(401, 'Auth.SessionExpired');

    it('refreshes the session and replays the action', async () => {
      const fixture = await open();

      button(fixture, `Renvoyer l'invitation à new@saas.local`)!.click();
      const url = `${INVITATIONS_URL}/${BY_OWNER.id}/resend`;
      httpMock.expectOne({ method: 'POST', url }).flush(expired.body, expired.options);
      httpMock.expectOne('/api/v1/auth/refresh').flush({
        accessToken: fakeAccessToken(OWNER_ID, 'Owner'),
        expiresAt: '2030-01-01T00:00:00Z',
      });
      httpMock.expectOne({ method: 'POST', url }).flush(null, NO_CONTENT);
      await reload(fixture, INVITATIONS_URL, INVITATIONS);

      expect(status(fixture)).toBe('Invitation renvoyée à new@saas.local.');
      expect(navigate).not.toHaveBeenCalled();
    });

    // Rétrogradé en Member : le refresh donne un jeton Member, la requête rejouée reçoit 403, et
    // l'écran, qui n'est plus permis, renvoie aux produits.
    it('leaves for the products when the refreshed role no longer allows the screen', async () => {
      const fixture = await open();

      button(fixture, `Révoquer l'invitation de new@saas.local`)!.click();
      const url = `${INVITATIONS_URL}/${BY_OWNER.id}`;
      httpMock.expectOne({ method: 'DELETE', url }).flush(expired.body, expired.options);
      httpMock.expectOne('/api/v1/auth/refresh').flush({
        accessToken: fakeAccessToken(OWNER_ID, 'Member'),
        expiresAt: '2030-01-01T00:00:00Z',
      });
      httpMock
        .expectOne({ method: 'DELETE', url })
        .flush(null, { status: 403, statusText: 'Forbidden' });
      await reload(fixture, INVITATIONS_URL, INVITATIONS);

      expect(alerts(fixture)).toContain('Votre rôle ne permet pas cette action.');
      expect(navigate).toHaveBeenCalledWith(['/products'], {});
      expect(snackBar()?.textContent).toContain(
        "Votre rôle ne donne plus accès à l'écran Organisation.",
      );
    });

    // « Déconnexion » : App mène à la connexion ; l'écran n'annonce pas une session expirée.
    it('leaves quietly on logout', async () => {
      const fixture = await open();

      TestBed.inject(AuthService).logout().subscribe();
      httpMock.expectOne({ method: 'POST', url: '/api/v1/auth/logout' }).flush(null, NO_CONTENT);
      await fixture.whenStable();

      expect(navigate).not.toHaveBeenCalled();
      expect(snackBar()).toBeNull();
    });

    // Retiré de l'organisation, ou refresh impossible (429, 503…) : la session est effacée, et
    // l'écran renvoie à la connexion.
    it.each([
      [401, 'Auth.SessionExpired'],
      [503, undefined],
    ])('leaves for the login page when the refresh fails with %s', async (code, name) => {
      const fixture = await open();

      button(fixture, `Renvoyer l'invitation à new@saas.local`)!.click();
      const url = `${INVITATIONS_URL}/${BY_OWNER.id}/resend`;
      httpMock.expectOne({ method: 'POST', url }).flush(expired.body, expired.options);
      const refusal = problem(code, name);
      httpMock.expectOne('/api/v1/auth/refresh').flush(refusal.body, refusal.options);
      await fixture.whenStable();

      expect(navigate).toHaveBeenCalledWith(['/login'], {
        queryParams: { returnUrl: '/organization' },
      });
      expect(snackBar()?.textContent).toContain('Votre session a expiré. Reconnectez-vous.');
    });
  });
});
