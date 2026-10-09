# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) + Conventional Commits.
Convention spécifique : chaque entrée de release comporte une rubrique **Migrations DB**
(même vide : « aucune ») — impossible d'oublier un `database update` au déploiement.

## [Unreleased]

### Added
- Timeouts de requête globaux (`RequestTimeouts:DefaultSeconds`, 30 s par défaut) et output cache opt-in strict (aucune policy par défaut, activation par endpoint via `.CacheOutput(...)`).
- Cache HybridCache de la lecture d'un produit, avec clé et tag propres au tenant, invalidé à la modification et à la suppression.
- Tests Playwright de la page de connexion, API simulée, avec un contrôle axe des règles WCAG 2.2 A et AA : toute violation détectée fait échouer le test (axe ne vérifie qu'une partie des critères WCAG).
- Job Hangfire quotidien `purge-processed-outbox-messages` : supprime les messages de l'outbox traités depuis plus de 7 jours.
- Mutation testing Stryker 5 (runner MTP) : seuil bloquant sur le Domain, score mesuré sur l'Application.
- Quickstart rejouable de bout en bout : API lancée par `dotnet run`, clé JWT en user-secrets, API conteneurisée derrière le profil Compose `full`.
- Contrat OpenAPI écrit au build dans `web-angular/openapi.json` et comparé par la CI au fichier versionné.
- Tests unitaires des validateurs et des handlers qui portent une logique ; `ErrorRegistryTests` compare `docs/error-codes.md` au code.

### Changed
- Tests sur le runner natif Microsoft Testing Platform (xunit.v3) ; couverture produite par `Microsoft.Testing.Extensions.CodeCoverage` (format Cobertura).
- Angular : retrait de `withFetch()`, de `provideAnimationsAsync()` et du package `@angular/animations`, inutiles en v22.
- Actions GitHub mises à jour, épinglées par SHA avec leur version exacte en commentaire.
- Dependabot : mises à jour groupées, délai de 7 jours avant de proposer une version, montées majeures d'Angular et de TypeScript écartées (TypeScript suit la version exigée par Angular).
- Workflows déclenchés sur `main` uniquement.
- Domain sans framework : `ApplicationUser` et `RefreshToken` passent dans l'Infrastructure, `IDomainEvent` ne dépend plus de Mediator. Les handlers de domain events implémentent `IDomainEventHandler<TEvent>`.
- Outbox : une transaction par message, lu avec `FOR UPDATE SKIP LOCKED` ; chaque handler dans son propre scope ; handlers déjà passés non rappelés lors d'un nouvel essai (`OutboxMessageConsumers`) ; nouvel essai après 1, 4, 16 puis 64 minutes, puis abandon journalisé en erreur ; un arrêt de l'application ne compte pas de tentative et ne journalise pas d'erreur. L'e-mail de bienvenue n'avale plus les erreurs SMTP : il est retenté.
- Refus métier (e-mail non confirmé, lien de réinitialisation invalide, changement de son propre rôle, mot de passe manquant à l'acceptation) renvoyés en erreurs ErrorOr au lieu de `ValidationException`.
- Contrat HTTP : contrats de requête dédiés au lieu des commandes de l'Application ; Bearer exigé seulement sur les routes authentifiées ; pas de `Location` vers une route inexistante.
- Empreintes de tokens en hexadécimal minuscule partout : les refresh tokens existants ne sont plus reconnus, une reconnexion est nécessaire.

### Fixed
- gitleaks : la configuration du dépôt remplaçait les règles par défaut et ne détectait plus rien ; elle les étend maintenant. Le hook pre-commit bloque dès que gitleaks est installé.
- Job gitleaks en échec sur les pull requests (permission `pull-requests: read` manquante).
- `.env.example` : nom de variable SMTP corrigé (`Email__UseSslOnConnect`), variables inutilisées retirées.
- `OutboxProcessor` utilise `IDateTimeProvider` (testabilité).
- Une erreur de validation produisait trois logs d'erreur, dont un faux 500 ; elle est maintenant journalisée une fois, en 400.
- Les logs n'atteignaient pas le provider OpenTelemetry : Serilog les transmet maintenant aux providers enregistrés.

### Removed
- Package `Microsoft.Extensions.Http.Resilience`, référencé mais jamais utilisé.

### Security
- Connexion : même coût de hash pour un e-mail inconnu ou un compte verrouillé que pour un mauvais mot de passe (oracle de timing fermé).
- Un jeton de confirmation ou de réinitialisation mal formé renvoie 400 au lieu de 500.
- Un utilisateur retiré d'une organisation perd ses sessions sur celle-ci (le rafraîchissement lui accordait le rôle Member) ; ses sessions sur ses autres organisations continuent. Un refresh refusé supprime le cookie.
- Une réinitialisation de mot de passe révoque toutes les sessions ouvertes.
- Organisation par défaut à la connexion : la plus ancienne adhésion, au lieu d'un ordre SQL indéterminé.
- `TenantId`, l'audit et la suppression logique n'ont plus de setter public ; l'intercepteur couvre aussi `SaveChanges` synchrone et refuse un changement de tenant.
- Routes `/organizations/{orgId}` : contrôle de portée centralisé dans `OrganizationScopeFilter` ; test qui vérifie le filtre tenant de chaque requête Dapper.
- Rôle Admin effectif (invitations, suppression de produits) ; changement de rôle réservé à l'Owner. Les tests appliquent les vraies policies au lieu d'une copie.

### Migrations DB
- `RemoveUserDisplayName` : supprime la colonne inutilisée `AspNetUsers.DisplayName`.
- `OutboxConsumersAndRetries` : crée la table `OutboxMessageConsumers`, ajoute `OutboxMessages.NextAttemptOn` et l'index partiel des messages à traiter.
