import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpContext, HttpErrorResponse, httpResource } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { form, FormField, required, email } from '@angular/forms/signals';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import type { InvitationDto, MemberDto } from '../../api/generated';
import { AuthService } from '../../core/auth/auth.service';
import { SKIP_ERROR_TOAST } from '../../core/error/http-error.interceptor';
import { InvitableRole, OrganizationService } from './organization.service';
import { problemMessage } from './problem-message';
import { FormErrors } from '../../core/forms/form-errors';
import { TableScroll } from '../../core/layout/table-scroll';
import {
  RemoveMemberDecision,
  RemoveMemberDialog,
  RemoveMemberDialogData,
} from './remove-member.dialog';

const INVITABLE_ROLES: InvitableRole[] = ['Member', 'Admin'];
const ALREADY_PENDING = 'Une invitation est déjà en attente pour cette adresse.';

function isInvitableRole(value: string): value is InvitableRole {
  return value === 'Member' || value === 'Admin';
}

function codeOf(err: unknown): string | undefined {
  return err instanceof HttpErrorResponse && typeof err.error === 'object' && err.error !== null
    ? (err.error as { code?: string }).code
    : undefined;
}

/** Une révocation demandée avec un retrait, et son refus éventuel. */
interface Revocation {
  invitation: InvitationDto;
  error: string | null;
}

/**
 * Écran Organisation, pour l'Owner et l'Admin. Membres : lecture pour les deux, changement de rôle
 * et retrait pour l'Owner seul, jamais sur son propre rôle ni pour un Owner. Invitations en
 * attente : liste, envoi, renvoi et révocation. L'API contrôle chaque appel ; l'écran masque les
 * actions que le rôle du jeton ne permet pas et affiche les refus selon leur code.
 */
@Component({
  selector: 'app-organization',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormField,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    TableScroll,
  ],
  templateUrl: './organization.page.html',
  styleUrl: './organization.page.scss',
})
export class OrganizationPage {
  private readonly auth = inject(AuthService);
  private readonly organization = inject(OrganizationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly roles = INVITABLE_ROLES;
  protected readonly session = this.auth.session;
  protected readonly isOwner = computed(() => this.session()?.tenantRole === 'Owner');
  private readonly orgId = computed(() => this.session()?.tenantId ?? '');

  // L'écran affiche lui-même les erreurs de chargement : pas de toast global.
  protected readonly members = httpResource<MemberDto[]>(() =>
    this.orgId()
      ? {
          url: OrganizationService.membersUrl(this.orgId()),
          context: new HttpContext().set(SKIP_ERROR_TOAST, true),
        }
      : undefined,
  );
  protected readonly invitations = httpResource<InvitationDto[]>(() =>
    this.orgId()
      ? {
          url: OrganizationService.invitationsUrl(this.orgId()),
          context: new HttpContext().set(SKIP_ERROR_TOAST, true),
        }
      : undefined,
  );
  // Membres connus, pour nommer l'expéditeur d'une invitation ; `null` tant que la liste n'est
  // pas lue (value() lève une erreur si son chargement a échoué).
  private readonly knownMembers = computed(() =>
    this.members.hasValue() ? this.members.value() : null,
  );

  protected readonly memberColumns = computed(() =>
    this.isOwner() ? ['email', 'role', 'joinedAt', 'actions'] : ['email', 'role', 'joinedAt'],
  );
  protected readonly invitationColumns = ['email', 'role', 'invitedBy', 'expiresAt', 'actions'];

  /** Clé de l'action en cours (`role:<userId>`, `remove:<userId>`, `resend:<id>`…). */
  protected readonly busy = signal<string | null>(null);
  // Une action attend aussi la relecture qui la suit : une ligne déjà révoquée ne se clique pas.
  protected readonly membersLocked = computed(
    () => this.busy() !== null || this.members.isLoading(),
  );
  protected readonly invitationsLocked = computed(
    () => this.busy() !== null || this.invitations.isLoading(),
  );
  protected readonly status = signal<string | null>(null);
  protected readonly membersError = signal<string | null>(null);
  protected readonly invitationsError = signal<string | null>(null);
  /** Rôle choisi dans la liste, pas encore appliqué, par utilisateur. */
  protected readonly pendingRoles = signal<Record<string, InvitableRole>>({});

  protected readonly inviteModel = signal<{ email: string; role: InvitableRole }>({
    email: '',
    role: 'Member',
  });
  protected readonly inviteForm = form(this.inviteModel, (p) => {
    required(p.email, { message: 'Adresse e-mail requise' });
    email(p.email, { message: 'Adresse e-mail invalide' });
  });
  /** Erreurs sous le champ modifié puis quitté, ou après un envoi refusé. */
  protected readonly inviteErrors = new FormErrors();
  protected readonly inviteError = signal<string | null>(null);

  private readonly membersHeading = viewChild<ElementRef<HTMLElement>>('membersHeading');
  private readonly invitationsHeading = viewChild<ElementRef<HTMLElement>>('invitationsHeading');
  private readonly inviteEmail = viewChild<ElementRef<HTMLInputElement>>('inviteEmail');

  constructor() {
    // La session peut changer pendant que l'écran est ouvert : un refresh refusé l'efface (retrait
    // de l'organisation, par exemple), un refresh peut abaisser le rôle. L'écran ne reste pas
    // ouvert sur des actions que la session ne permet plus. Après « Déconnexion », App mène déjà à
    // la connexion : pas de message d'expiration.
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        if (!this.auth.loggedOut()) {
          this.leave('Votre session a expiré. Reconnectez-vous.', '/login', {
            returnUrl: '/organization',
          });
        }
      } else if (!this.auth.canManageOrganization()) {
        this.leave("Votre rôle ne donne plus accès à l'écran Organisation.", '/products');
      }
    });
  }

  // Le contrôle sur l'utilisateur lui-même vaut aussi quand la liste le montre avec un autre rôle
  // que son jeton (rôle changé depuis l'émission du jeton).
  protected canChangeRole(member: MemberDto): boolean {
    return this.isOwner() && member.role !== 'Owner' && member.userId !== this.session()?.userId;
  }

  protected canRemove(member: MemberDto): boolean {
    return this.isOwner() && member.role !== 'Owner' && member.userId !== this.session()?.userId;
  }

  protected selectedRole(member: MemberDto): string {
    return this.pendingRoles()[member.userId] ?? member.role;
  }

  protected chooseRole(member: MemberDto, event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    if (!isInvitableRole(value)) return;
    this.pendingRoles.update((pending) => ({ ...pending, [member.userId]: value }));
  }

  /**
   * Expéditeur d'une invitation : « Vous », l'e-mail d'un membre, « Ancien membre » s'il n'est plus
   * dans la liste, ou « — » tant que la liste des membres n'est pas lue.
   */
  protected invitedBy(invitation: InvitationDto): string {
    if (invitation.invitedByUserId === this.session()?.userId) return 'Vous';
    const members = this.knownMembers();
    if (!members) return '—';
    return members.find((m) => m.userId === invitation.invitedByUserId)?.email ?? 'Ancien membre';
  }

  protected loadError(error: unknown): string {
    return problemMessage(error);
  }

  applyRole(member: MemberDto): void {
    const role = this.pendingRoles()[member.userId];
    if (!role || role === member.role) return;
    this.start(`role:${member.userId}`);
    this.organization.changeRole(this.orgId(), member.userId, role).subscribe({
      next: () => {
        // Pendant sa relecture, la liste montre le rôle accepté, pas l'ancien.
        if (this.members.hasValue()) {
          this.members.update((list) =>
            list?.map((m) => (m.userId === member.userId ? { ...m, role } : m)),
          );
        }
        this.forgetPendingRole(member.userId);
        this.done(`Rôle de ${member.email} : ${role}.`, this.members, this.membersHeading);
      },
      error: (err: unknown) => {
        this.forgetPendingRole(member.userId);
        this.failed(this.membersError, problemMessage(err), this.members, this.membersHeading);
      },
    });
  }

  // Les invitations du membre sont relues à l'ouverture de la confirmation : la liste affichée
  // peut être en cours de chargement, en échec ou périmée. Pendant cette lecture, le bouton
  // « Retirer » est désactivé et perd le focus : la confirmation le lui rend en se fermant. Si la
  // page est quittée avant la réponse, la confirmation ne s'ouvre pas.
  confirmRemoval(member: MemberDto, event: Event): void {
    const trigger = event.currentTarget instanceof HTMLElement ? event.currentTarget : true;
    this.start(`remove:${member.userId}`);
    this.organization
      .pendingInvitations(this.orgId())
      .pipe(
        map((all) => all.filter((i) => i.invitedByUserId === member.userId)),
        catchError(() => of(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((sent) => {
        this.busy.set(null);
        this.dialog
          .open<RemoveMemberDialog, RemoveMemberDialogData, RemoveMemberDecision>(
            RemoveMemberDialog,
            {
              data: { member, invitations: sent },
              autoFocus: 'first-tabbable',
              restoreFocus: trigger,
            },
          )
          .afterClosed()
          .subscribe((decision) => {
            if (decision) this.remove(member, decision.revokeInvitations ? (sent ?? []) : []);
          });
      });
  }

  invite(event: Event): void {
    event.preventDefault();
    if (!this.inviteErrors.check(this.inviteForm)) return;
    if (this.invitationsLocked()) return;
    const { email: address, role } = this.inviteModel();
    this.start('invite');
    this.organization.invite(this.orgId(), address, role).subscribe({
      next: () => {
        this.inviteForm().reset({ email: '', role: 'Member' });
        this.inviteErrors.clear();
        this.done(`Invitation envoyée à ${address}.`, this.invitations, this.inviteEmail);
      },
      // Un 409 sans code vient de l'index unique des invitations en attente : même sens que
      // Invitation.AlreadyPending.
      error: (err: unknown) =>
        this.failed(
          this.inviteError,
          problemMessage(err, ALREADY_PENDING),
          this.invitations,
          this.inviteEmail,
        ),
    });
  }

  resend(invitation: InvitationDto): void {
    this.start(`resend:${invitation.id}`);
    this.organization.resend(this.orgId(), invitation.id).subscribe({
      next: () =>
        this.done(
          `Invitation renvoyée à ${invitation.email}.`,
          this.invitations,
          this.invitationsHeading,
        ),
      error: (err: unknown) =>
        this.failed(
          this.invitationsError,
          problemMessage(err),
          this.invitations,
          this.invitationsHeading,
        ),
    });
  }

  revoke(invitation: InvitationDto): void {
    this.start(`revoke:${invitation.id}`);
    this.organization.revoke(this.orgId(), invitation.id).subscribe({
      next: () =>
        this.done(
          `Invitation pour ${invitation.email} révoquée.`,
          this.invitations,
          this.invitationsHeading,
        ),
      error: (err: unknown) =>
        this.failed(
          this.invitationsError,
          problemMessage(err),
          this.invitations,
          this.invitationsHeading,
        ),
    });
  }

  // Retrait d'abord, puis révocation des invitations choisies. Si le retrait est refusé, rien
  // d'autre n'est fait ; si le membre était déjà parti (Membership.NotFound), ses invitations sont
  // tout de même révoquées. Une révocation refusée n'annule pas le retrait : le message donne
  // l'adresse et la raison.
  private remove(member: MemberDto, toRevoke: InvitationDto[]): void {
    this.start(`remove:${member.userId}`);
    this.organization.removeMember(this.orgId(), member.userId).subscribe({
      next: () =>
        this.revokeAfterRemoval(`${member.email} a été retiré de l'organisation.`, toRevoke),
      error: (err: unknown) => {
        if (codeOf(err) === 'Membership.NotFound') {
          this.revokeAfterRemoval(problemMessage(err), toRevoke);
          return;
        }
        this.failed(this.membersError, problemMessage(err), this.members, this.membersHeading);
      },
    });
  }

  private revokeAfterRemoval(removed: string, toRevoke: InvitationDto[]): void {
    const revocations: Observable<Revocation>[] = toRevoke.map((invitation) =>
      this.organization.revoke(this.orgId(), invitation.id).pipe(
        map(() => ({ invitation, error: null })),
        catchError((err: unknown) => of({ invitation, error: problemMessage(err) })),
      ),
    );
    (revocations.length > 0 ? forkJoin(revocations) : of([] as Revocation[])).subscribe(
      (results) => {
        if (this.destroyRef.destroyed) return;
        const failures = results.filter((r) => r.error !== null);
        const revoked = results.length - failures.length;
        this.invitations.reload();
        const message = revoked > 0 ? `${removed} Invitations révoquées : ${revoked}.` : removed;
        if (failures.length === 0) {
          this.done(message, this.members, this.membersHeading);
          return;
        }
        this.status.set(message);
        this.failed(
          this.invitationsError,
          'Invitations non révoquées : ' +
            failures.map((f) => `${f.invitation.email} (${f.error})`).join(', ') +
            '. Vérifiez les listes des membres et des invitations.',
          this.members,
          this.invitationsHeading,
        );
      },
    );
  }

  private forgetPendingRole(userId: string): void {
    this.pendingRoles.update((pending) => {
      const others = { ...pending };
      delete others[userId];
      return others;
    });
  }

  private start(action: string): void {
    this.busy.set(action);
    this.status.set(null);
    this.membersError.set(null);
    this.invitationsError.set(null);
    this.inviteError.set(null);
  }

  // Une action envoyée va à son terme même si la page est quittée entre-temps (un retrait révoque
  // encore les invitations choisies) ; seul l'affichage de son résultat n'a plus lieu.
  private done(
    message: string,
    toReload: { reload(): boolean },
    focusTarget: () => ElementRef<HTMLElement> | undefined,
  ): void {
    if (this.destroyRef.destroyed) return;
    this.busy.set(null);
    this.status.set(message);
    toReload.reload();
    this.focusAfterRender(focusTarget);
  }

  // Après un refus, la liste est relue : l'état affiché (un rôle, une invitation déjà révoquée…)
  // peut avoir changé ailleurs.
  private failed(
    target: { set(value: string | null): void },
    message: string,
    toReload: { reload(): boolean },
    focusTarget: () => ElementRef<HTMLElement> | undefined,
  ): void {
    if (this.destroyRef.destroyed) return;
    this.busy.set(null);
    target.set(message);
    toReload.reload();
    this.focusAfterRender(focusTarget);
  }

  // Le bouton de l'action est désactivé puis la ligne redessinée : le focus irait sur la page. Il
  // va au titre de la section concernée (ou au champ e-mail après une invitation), que suivent le
  // message et la liste relue.
  private focusAfterRender(target: () => ElementRef<HTMLElement> | undefined): void {
    afterNextRender(() => target()?.nativeElement.focus(), { injector: this.injector });
  }

  private leave(message: string, url: string, queryParams?: Record<string, string>): void {
    this.dialog.closeAll();
    this.snackBar.open(message, 'Fermer', { duration: 6000 });
    void this.router.navigate([url], queryParams ? { queryParams } : {});
  }
}
