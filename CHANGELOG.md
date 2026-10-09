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

### Removed
- Package `Microsoft.Extensions.Http.Resilience`, référencé mais jamais utilisé.

### Migrations DB
- (aucune)
