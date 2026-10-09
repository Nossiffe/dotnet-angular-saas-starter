# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) + Conventional Commits.
Convention spécifique : chaque entrée de release comporte une rubrique **Migrations DB**
(même vide : « aucune ») — impossible d'oublier un `database update` au déploiement.

## [Unreleased]

### Added
- Timeouts de requête globaux (`RequestTimeouts:DefaultSeconds`, 30 s par défaut) et output cache opt-in strict (aucune policy par défaut, activation par endpoint via `.CacheOutput(...)`).
- Cache déclaratif `[CachedQuery]` : le `CachingBehavior` du pipeline Mediator met en cache les queries marquées dans HybridCache, avec clé et tags propres au tenant (invalidation logique par tag à l'écriture) ; adopté par la slice Products.
- Tests Playwright de la page de connexion, API simulée, avec contrôle d'accessibilité axe (WCAG 2.2 AA).
- Mutation testing Stryker 5 (runner MTP) : seuil bloquant sur le Domain, score mesuré sur l'Application.
- Quickstart rejouable de bout en bout : API lancée par `dotnet run`, clé JWT en user-secrets, API conteneurisée derrière le profil Compose `full`.

### Changed
- Tests sur le runner natif Microsoft Testing Platform (xunit.v3) ; couverture produite par `Microsoft.Testing.Extensions.CodeCoverage` (format Cobertura).
- Angular : retrait de `withFetch()`, de `provideAnimationsAsync()` et du package `@angular/animations`, inutiles en v22.
- Actions GitHub mises à jour, épinglées par SHA avec leur version exacte en commentaire.
- Dependabot : mises à jour groupées, délai de 7 jours avant de proposer une version, montées majeures d'Angular et de TypeScript écartées (TypeScript suit la version exigée par Angular).
- Workflows déclenchés sur `main` uniquement.

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
- Le refresh token est refusé et révoqué si l'utilisateur n'est plus membre de l'organisation (il recevait le rôle Member).
- Une réinitialisation de mot de passe révoque toutes les sessions ouvertes.
- Organisation par défaut à la connexion : la plus ancienne adhésion, au lieu d'un ordre SQL indéterminé.
- `TenantId`, l'audit et la suppression logique n'ont plus de setter public ; l'intercepteur couvre aussi `SaveChanges` synchrone et refuse un changement de tenant.
- Routes `/organizations/{orgId}` : contrôle de portée centralisé dans `OrganizationScopeFilter` ; test qui vérifie le filtre tenant de chaque requête Dapper.
- Rôle Admin effectif (invitations, suppression de produits) ; changement de rôle réservé à l'Owner. Les tests appliquent les vraies policies au lieu d'une copie.

### Migrations DB
- (aucune)
