import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import type {
  ChangeMemberRoleRequest,
  InvitationDto,
  InviteMemberRequest,
  InviteResponse,
} from '../../api/generated';
import { SKIP_ERROR_TOAST } from '../../core/error/http-error.interceptor';

export type InvitableRole = 'Member' | 'Admin';

// L'écran Organisation affiche lui-même ses erreurs, selon leur code : pas de toast global. Un 401
// passe toujours par le refresh de authInterceptor, indépendant de ce contexte.
const pageHandlesErrors = () => ({ context: new HttpContext().set(SKIP_ERROR_TOAST, true) });

@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly http = inject(HttpClient);

  static membersUrl(orgId: string): string {
    return `/api/v1/organizations/${orgId}/members`;
  }

  static invitationsUrl(orgId: string): string {
    return `/api/v1/organizations/${orgId}/invitations`;
  }

  /** Lecture fraîche des invitations en attente, au moment d'une décision (retrait d'un membre). */
  pendingInvitations(orgId: string): Observable<InvitationDto[]> {
    return this.http.get<InvitationDto[]>(
      OrganizationService.invitationsUrl(orgId),
      pageHandlesErrors(),
    );
  }

  invite(orgId: string, email: string, role: InvitableRole): Observable<InviteResponse> {
    const body: InviteMemberRequest = { email, role };
    return this.http.post<InviteResponse>(
      OrganizationService.invitationsUrl(orgId),
      body,
      pageHandlesErrors(),
    );
  }

  resend(orgId: string, invitationId: string): Observable<void> {
    return this.http.post<void>(
      `${OrganizationService.invitationsUrl(orgId)}/${invitationId}/resend`,
      {},
      pageHandlesErrors(),
    );
  }

  revoke(orgId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(
      `${OrganizationService.invitationsUrl(orgId)}/${invitationId}`,
      pageHandlesErrors(),
    );
  }

  changeRole(orgId: string, userId: string, role: InvitableRole): Observable<void> {
    const body: ChangeMemberRoleRequest = { role };
    return this.http.put<void>(
      `${OrganizationService.membersUrl(orgId)}/${userId}/role`,
      body,
      pageHandlesErrors(),
    );
  }

  removeMember(orgId: string, userId: string): Observable<void> {
    return this.http.delete<void>(
      `${OrganizationService.membersUrl(orgId)}/${userId}`,
      pageHandlesErrors(),
    );
  }
}
