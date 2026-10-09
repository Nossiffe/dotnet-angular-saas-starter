import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { AcceptInvitationResponse, PostApiV1InvitationsAcceptData } from '../../api/generated';
import { SKIP_ERROR_TOAST } from '../../core/error/http-error.interceptor';

// Corps de l'acceptation = DTO généré `AcceptInvitationRequest` (via le type Hey API).
export type AcceptInvitationInput = PostApiV1InvitationsAcceptData['body'];

@Injectable({ providedIn: 'root' })
export class InvitationsService {
  private readonly http = inject(HttpClient);

  // La page d'acceptation affiche elle-même ses erreurs : pas de toast global.
  accept(body: AcceptInvitationInput): Observable<AcceptInvitationResponse> {
    return this.http.post<AcceptInvitationResponse>('/api/v1/invitations/accept', body, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true),
    });
  }
}
