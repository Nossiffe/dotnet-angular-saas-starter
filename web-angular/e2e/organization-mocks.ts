import type { Page, Route } from '@playwright/test';

export const ORG_ID = '0199c9a0-0000-7000-8000-0000000000aa';
export const OWNER_ID = '0199c9a0-0000-7000-8000-000000000001';
export const ADMIN_ID = '0199c9a0-0000-7000-8000-000000000002';
export const MEMBER_ID = '0199c9a0-0000-7000-8000-000000000003';

type Role = 'Owner' | 'Admin' | 'Member';

interface Member {
  userId: string;
  email: string;
  role: Role;
  joinedAt: string;
}

interface Invitation {
  id: string;
  email: string;
  role: 'Member' | 'Admin';
  invitedByUserId: string;
  expiresAt: string;
  createdAt: string;
}

/** A call the page made to the mocked API, in order. */
export interface Call {
  method: string;
  path: string;
  body?: unknown;
}

// Access token for the front end, which reads its claims without checking the signature.
function accessToken(userId: string, role: Role): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
  return [
    encode({ alg: 'HS256', typ: 'JWT' }),
    encode({ sub: userId, tenant: ORG_ID, tenant_role: role, email: 'me@saas.local' }),
    'signature',
  ].join('.');
}

const problem = (route: Route, status: number, code: string) =>
  route.fulfill({
    status,
    contentType: 'application/problem+json',
    body: JSON.stringify({ status, title: '…', code, errors: { [code]: ['…'] } }),
  });

/**
 * The organization API as an in-memory store: the start-up session refresh signs the user in
 * with the given role, and the members and invitations routes read and change the store like the
 * real API. Returns the calls made, for the assertions.
 */
export async function mockOrganization(page: Page, role: Role, userId = OWNER_ID): Promise<Call[]> {
  const calls: Call[] = [];
  const members: Member[] = [
    {
      userId: OWNER_ID,
      email: 'owner@saas.local',
      role: 'Owner',
      joinedAt: '2026-10-01T09:00:00Z',
    },
    {
      userId: ADMIN_ID,
      email: 'admin@saas.local',
      role: 'Admin',
      joinedAt: '2026-10-02T09:00:00Z',
    },
    {
      userId: MEMBER_ID,
      email: 'member@saas.local',
      role: 'Member',
      joinedAt: '2026-10-03T09:00:00Z',
    },
  ];
  const invitations: Invitation[] = [
    {
      id: '0199c9a0-0000-7000-8000-0000000000b1',
      email: 'new@saas.local',
      role: 'Member',
      invitedByUserId: OWNER_ID,
      expiresAt: '2026-10-17T09:00:00Z',
      createdAt: '2026-10-10T09:00:00Z',
    },
    {
      id: '0199c9a0-0000-7000-8000-0000000000b2',
      email: 'admin.other@saas.local',
      role: 'Admin',
      invitedByUserId: ADMIN_ID,
      expiresAt: '2026-10-17T09:00:00Z',
      createdAt: '2026-10-10T09:00:00Z',
    },
  ];

  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({
      json: { accessToken: accessToken(userId, role), expiresAt: '2030-01-01T00:00:00Z' },
    }),
  );
  await page.route('**/api/v1/auth/logout', (route) => route.fulfill({ status: 204 }));
  await page.route('**/api/v1/products', (route) =>
    route.fulfill({ json: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } }),
  );
  await page.route(`**/api/v1/organizations/${ORG_ID}/**`, async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname.replace(`/api/v1/organizations/${ORG_ID}`, '');
    const method = request.method();
    calls.push({ method, path, body: request.postDataJSON() ?? undefined });
    const [, collection, id, action] = path.split('/');

    if (collection === 'members') {
      if (method === 'GET') return route.fulfill({ json: members });
      const member = members.find((m) => m.userId === id);
      if (!member) return problem(route, 404, 'Membership.NotFound');
      if (method === 'PUT' && action === 'role') {
        member.role = (request.postDataJSON() as { role: Role }).role;
        return route.fulfill({ status: 204 });
      }
      if (method === 'DELETE') {
        if (member.role === 'Owner') return problem(route, 400, 'Membership.CannotRemoveOwner');
        members.splice(members.indexOf(member), 1);
        return route.fulfill({ status: 204 });
      }
    }

    if (collection === 'invitations') {
      if (method === 'GET') return route.fulfill({ json: invitations });
      if (method === 'POST' && !id) {
        const body = request.postDataJSON() as { email: string; role: 'Member' | 'Admin' };
        if (invitations.some((i) => i.email === body.email)) {
          return problem(route, 409, 'Invitation.AlreadyPending');
        }
        const created = {
          id: `0199c9a0-0000-7000-8000-0000000000c${invitations.length}`,
          ...body,
          invitedByUserId: userId,
          expiresAt: '2026-10-17T09:00:00Z',
          createdAt: '2026-10-10T10:00:00Z',
        };
        invitations.unshift(created);
        return route.fulfill({ status: 201, json: { invitationId: created.id } });
      }
      const invitation = invitations.find((i) => i.id === id);
      if (!invitation) return problem(route, 404, 'Invitation.NotFound');
      if (method === 'POST' && action === 'resend') return route.fulfill({ status: 204 });
      if (method === 'DELETE') {
        invitations.splice(invitations.indexOf(invitation), 1);
        return route.fulfill({ status: 204 });
      }
    }

    return route.fulfill({ status: 404 });
  });
  return calls;
}
