import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import type { InvitationDto, MemberDto } from '../../api/generated';

export interface RemoveMemberDialogData {
  member: MemberDto;
  /** Invitations en attente envoyées par ce membre, lues à l'ouverture ; `null` si illisibles. */
  invitations: InvitationDto[] | null;
}

/** Réponse de la confirmation ; `undefined` si l'utilisateur annule. */
export interface RemoveMemberDecision {
  revokeInvitations: boolean;
}

/**
 * Confirmation du retrait d'un membre. Les invitations encore en attente qu'il a envoyées sont
 * listées, avec leur révocation proposée par défaut : sinon, un Admin retiré qui s'est invité sous
 * une autre adresse pourrait revenir en acceptant cette invitation. Si elles n'ont pas pu être
 * lues, la confirmation le dit.
 */
@Component({
  selector: 'app-remove-member-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule, MatCheckboxModule],
  styles: `
    .warning {
      color: var(--mat-sys-error);
      font-weight: 500;
    }
  `,
  template: `
    <h2 mat-dialog-title>Retirer {{ data.member.email }} ?</h2>
    <mat-dialog-content>
      <p>{{ data.member.email }} perdra immédiatement l'accès à l'organisation.</p>
      @if (data.invitations === null) {
        <p class="warning">
          Les invitations en attente n'ont pas pu être lues : vérifiez après le retrait que ce
          membre n'en a pas envoyé.
        </p>
      } @else if (data.invitations.length > 0) {
        <p id="sent-invitations">Invitations en attente envoyées par ce membre :</p>
        <ul aria-labelledby="sent-invitations">
          @for (invitation of data.invitations; track invitation.id) {
            <li>{{ invitation.email }} ({{ invitation.role }})</li>
          }
        </ul>
        <mat-checkbox
          [checked]="revokeInvitations()"
          (change)="revokeInvitations.set($event.checked)"
        >
          Les révoquer aussi
        </mat-checkbox>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close type="button">Annuler</button>
      <button mat-flat-button class="danger" type="button" [mat-dialog-close]="decision()">
        Retirer
      </button>
    </mat-dialog-actions>
  `,
})
export class RemoveMemberDialog {
  protected readonly data = inject<RemoveMemberDialogData>(MAT_DIALOG_DATA);
  protected readonly revokeInvitations = signal(true);

  protected decision(): RemoveMemberDecision {
    const sent = this.data.invitations ?? [];
    return { revokeInvitations: sent.length > 0 && this.revokeInvitations() };
  }
}
