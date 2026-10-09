# Registre des erreurs métier (ErrorOr)

Source **unique** des erreurs métier du backend.

## Convention

- Toute erreur métier est un membre **statique** d'une classe `XxxErrors`
  (`ProductErrors`, `OrganizationErrors`, `InvitationErrors`, `AuthErrors`) —
  **jamais** `Error.X(...)` inline dans un handler ou une factory.
- `Code` = `Entity.Reason` (PascalCase, point simple séparateur — ex. `Invitation.NotPending`).
- `Description` = phrase française **complète**, se terminant par un point, **sans**
  détail technique ni identifiant paramétré (pas de `$"Produit {id} introuvable"` —
  le `code` et l'`extensions.code` du problem+json portent déjà l'identification).

## Anti-énumération : cross-tenant = `NotFound`, jamais `Forbidden`

Une ressource qui existe mais appartient à un **autre tenant** doit renvoyer
`Error.NotFound` (404), jamais `Error.Forbidden` (403) — un 403 confirmerait
l'existence de la ressource à un tenant qui ne devrait même pas savoir qu'elle
existe.

Cas concret : `EndpointHelpers.CheckTenantScope` (vérifie qu'un `orgId` de route
correspond au tenant du token) réutilise **`OrganizationErrors.NotFound`** en cas de
mismatch — pas de nouvelle entrée dans le registre, c'est la même erreur « organisation
introuvable » qui s'applique, que l'organisation n'existe pas ou qu'elle appartienne à
un autre tenant.

## Registre (16 entrées)

Table à jour après les tasks 4-7 — miroir exact du `TheoryData` de
`src/tests/UnitTests/Common/DomainErrorsTests.cs`.

| Classe.Membre | Type | Code | Description |
|---|---|---|---|
| `ProductErrors.NotFound` | NotFound | `Product.NotFound` | Produit introuvable. |
| `OrganizationErrors.NotFound` | NotFound | `Organization.NotFound` | Organisation introuvable. |
| `OrganizationErrors.MemberNotFound` | NotFound | `Membership.NotFound` | Membre introuvable. |
| `OrganizationErrors.LastOwner` | Validation | `Organization.LastOwner` | Impossible de rétrograder le dernier Owner de l'organisation. |
| `OrganizationErrors.AlreadyMember` | Validation | `Membership.AlreadyMember` | Cette personne est déjà membre de l'organisation. |
| `InvitationErrors.NotFound` | NotFound | `Invitation.NotFound` | Invitation introuvable. |
| `InvitationErrors.OwnerRole` | Validation | `Invitation.OwnerRole` | Impossible d'inviter un membre avec le rôle Owner. |
| `InvitationErrors.AlreadyPending` | Conflict | `Invitation.AlreadyPending` | Une invitation est déjà en attente pour cet email. |
| `InvitationErrors.NotPendingRevoke` | Validation | `Invitation.NotPending` | Seule une invitation en attente peut être révoquée. |
| `InvitationErrors.NotPendingAccept` | Validation | `Invitation.NotPending` | Seule une invitation en attente peut être acceptée. |
| `InvitationErrors.NotPendingRefresh` | Validation | `Invitation.NotPending` | Seule une invitation en attente peut être renouvelée. |
| `InvitationErrors.Expired` | Validation | `Invitation.Expired` | Cette invitation a expiré. |
| `InvitationErrors.Invalid` | Validation | `Invitation.Invalid` | Invitation invalide ou expirée. |
| `AuthErrors.InvalidCredentials` | Unauthorized | `Auth.InvalidCredentials` | Identifiants invalides. |
| `AuthErrors.SessionExpired` | Unauthorized | `Auth.SessionExpired` | Session expirée, reconnectez-vous. |
| `AuthErrors.InvalidConfirmationLink` | Validation | `Auth.InvalidConfirmationLink` | Lien de confirmation invalide ou expiré. |

Notes :
- `InvitationErrors.AlreadyPending` est en **Conflict → 409** (pas Validation → 400) :
  aligné avec la branche 23505 (contrainte d'unicité Postgres) du
  `GlobalExceptionHandler`, qui protège la même règle en défense en profondeur
  contre une race entre le check applicatif et l'insert.
- `InvitationErrors.Invalid` est le message anti-énumération verbatim de
  `AcceptInvitationHandler` : **avant** vérification du token, un seul message
  (invitation inexistante, token faux, révoquée). Une fois le token valide
  (l'appelant détient la preuve), les messages précis `Expired`/`NotPending`
  s'appliquent — pas un oracle, comportement identique à l'avant-migration.
- Pas de `OrganizationErrors.AlreadyJoined` : le cas « déjà membre pendant l'accept »
  est un accept **idempotent** (retourne un succès), pas une erreur.

## Workflow « chercher avant d'inventer »

Avant d'ajouter une nouvelle erreur, vérifier qu'elle n'existe pas déjà :

```bash
grep -rn 'static Error ' src/ --include='*Errors.cs'
```

## Le test fait foi

`src/tests/UnitTests/Common/DomainErrorsTests.cs` (`DomainErrorsTests.Registre`, un
`TheoryData<Error, ErrorType, string>`) est la **source de vérité exécutable** du
registre : toute nouvelle erreur = une nouvelle ligne dans ce `TheoryData` (sinon
le point est relevé en revue de code).
