# Architecture

Starter SaaS .NET 10 + Angular 22, organisé en monorepo.

---

## Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────────┐
│  web-angular/   Angular 22 (signal-first, Material, Hey API)    │
└────────────────────────┬────────────────────────────────────────┘
                         │ HTTP/JSON (OpenAPI)
┌────────────────────────▼────────────────────────────────────────┐
│  src/Api           Minimal API — convention IEndpoint           │
├─────────────────────────────────────────────────────────────────┤
│  src/Application   CQRS use-cases (Mediator + FluentValidation) │
├─────────────────────────────────────────────────────────────────┤
│  src/Infrastructure  EF Core 10, Npgsql, Identity, JWT, Dapper  │
├─────────────────────────────────────────────────────────────────┤
│  src/Domain        Agrégats, value objects, domain events       │
└────────────────────────┬────────────────────────────────────────┘
                         │
                    PostgreSQL 17
```

---

## Couche Domain (pur — zéro dépendance framework)

### Règle de dépendance
```
Api → Infrastructure → Application → Domain
```
`Domain` ne référence que le runtime .NET, `Vogen` (identifiants typés) et `ErrorOr` (résultats
métier) : ni EF, ni ASP.NET, ni Identity, ni Mediator. `DependencyRuleTests` le vérifie sur les
références réelles de l'assembly. Le compte utilisateur (`ApplicationUser`, Identity) et le refresh
token vivent dans l'Infrastructure : le Domain ne connaît un utilisateur que par son `Guid`.

### Agrégats

| Agrégat | ID typé | Tenant-scoped | Interfaces |
|---|---|---|---|
| `Product` | `ProductId` | Oui (global query filter) | `IAuditable`, `ISoftDeletable`, `ITenantOwned` |
| `Organization` (+ entité `OrganizationMembership`) | `OrganizationId` | Non : c'est le tenant | `IAuditable` |
| `Invitation` | `InvitationId` | Rattachée à un `OrganizationId`, comparé à l'organisation de la requête par chaque handler | — |

**Conventions** :
- Constructeur privé (EF navigation).
- Factory statique (`Product.Create(name, price)`, `Invitation.Create(...)`). Un argument manquant lève
  `ArgumentException` (garde technique, la validation FluentValidation passe avant) ; une règle métier
  violée renvoie une erreur `ErrorOr` (`InvitationErrors.OwnerRole`).
- Setters privés — mutations uniquement via méthodes de comportement (`Rename`, `ChangePrice`, `AddMember`,
  `ChangeMemberRole`, `Accept`, `Revoke`).
- Domain events : l'agrégat appelle `Raise(new XxxDomainEvent(...))`. Persistés en transaction par `DomainEventsInterceptor` puis dispatchés de façon fiable par l'`OutboxProcessor` (voir « Robustesse » plus bas).

### Value objects

| Type | Mécanisme | EF |
|---|---|---|
| `Money` (montant + devise) | `record` C# | Owned type EF |
| `Slug` | Vogen `[ValueObject<string>]` | `ValueConverter<Slug, string>` manuel |
| `ProductId`, `OrganizationId`, `InvitationId` | Vogen `[ValueObject<Guid>]` | `ValueConverter<XxxId, Guid>` manuel |

**Recette Vogen** : `Conversions.SystemTextJson` uniquement dans Domain (STJ pour l'API), converter EF **à la main** dans Infrastructure (`ConfigureConventions`). Voir `docs/adr/0001`.

---

## Couche Application (use-cases CQRS)

### Structure

```
Application/<Feature>/<UseCase>/
  XxxCommand.cs     — record ICommand<T>  (write)
  XxxQuery.cs       — record IQuery<T>    (read)
  XxxHandler.cs     — ICommandHandler / IQueryHandler
  XxxValidator.cs   — AbstractValidator<T> (FluentValidation)
```

### Pipeline Mediator

```
Request → LoggingBehavior → ValidationBehavior → Handler
```

- `ValidationBehavior` exécute tous les `IValidator<T>` enregistrés automatiquement.
- Ne jamais valider manuellement dans un handler.

### Interfaces clés

| Interface | Responsabilité |
|---|---|
| `IProductRepository` | Charge/sauve l'agrégat `Product` (write) |
| `IOrganizationRepository` | Charge/sauve `Organization` (write) |
| `IInvitationRepository` | Charge/sauve `Invitation` (write) |
| `IUnitOfWork` | `SaveChangesAsync`, `ExecuteInTransactionAsync` |
| `IDbConnectionFactory` | Dapper — connexion pour les reads |
| `ICurrentUser` | UserId de l'utilisateur authentifié |
| `ITenantProvider` | TenantId lu dans le JWT (fail-closed), utilisé par EF et le cache |

---

## Couche Infrastructure

### Persistance — CQRS split

| Côté | Mécanisme | Où |
|---|---|---|
| **Write** | EF Core 10 via repositories explicites | `Repositories/ProductRepository.cs`, etc. |
| **Read** | Dapper — SQL direct → DTO | Handlers de query dans Application |

- `DbSet<T>` sont `internal` — pas accessibles depuis Application.
- Pas de `IRepository<T>` générique, pas d'`IAppDbContext`. Voir `docs/adr/0002`.
- `EnableRetryOnFailure` (5 tentatives, 10s) — résilience EF.

### Intercepteurs EF

| Intercepteur | Rôle |
|---|---|
| `AuditableEntityInterceptor` | Remplit `CreatedAt/By`, `UpdatedAt/By`, `TenantId` |
| `DomainEventsInterceptor` | Écrit les domain events des agrégats dans `OutboxMessages` pendant `SavingChanges`, dans la même transaction (voir « Transactional Outbox ») |

### Multi-tenant

- `TenantId` extrait du JWT claim `"tenant"` par `TenantProvider` (`Infrastructure/Identity/TenantProvider.cs`)
  — **fail-closed** : claim absent/invalide/vide → `InvalidOperationException` (pas de fallback
  `Guid.Empty`).
- **Named query filters EF Core 10, appliqués par convention** dans `AppDbContext.OnModelCreating` :
  toute entité `ITenantOwned` reçoit le filtre nommé `"TenantFilter"` (`e.TenantId == tenant.TenantId`),
  toute entité `ISoftDeletable` le filtre nommé `"SoftDeleteFilter"` (`!e.IsDeleted`) — aucune
  configuration au cas par cas par entité (pas de `HasQueryFilter` dans `XxxConfiguration.cs`).
  `"TenantFilter"` n'est jamais contournable métier ; `"SoftDeleteFilter"` peut être ignoré
  ponctuellement via `IgnoreQueryFilters(["SoftDeleteFilter"])`.
- L'`AuditableEntityInterceptor` injecte `TenantId` sur les nouveaux agrégats tenant-scoped, en
  `SaveChanges` comme en `SaveChangesAsync`, et refuse qu'une entité existante change de tenant.
  `TenantId`, l'audit et la suppression logique n'ont pas de setter public dans le Domain.
- **Lectures Dapper** : elles ne passent pas par les query filters EF, donc chaque requête filtre
  elle-même sur `"TenantId" = @TenantId`. `SecurityConventionTests`, dans les tests unitaires
  d'architecture, lit les raw string literals (`"""`) de `src/` et vérifie que chaque requête qui
  vise une table d'entité `ITenantOwned` contient ce filtre. Une requête écrite autrement (chaîne
  simple, concaténation) échappe au test : la convention est d'écrire le SQL en raw string literal.
- **Routes `/organizations/{orgId}/...`** : `OrganizationScopeFilter` compare l'`orgId` de la route
  au tenant du token et répond 404 sinon.

### Identité & Auth

- ASP.NET Core Identity (`ApplicationUser`, `IdentityRole<Guid>`, dans l'Infrastructure) stocké en Postgres.
- JWT maison : `JwtTokenService` (access token 15min) + `RefreshTokenStore` (refresh token en base).
- **Refresh token transporté par cookie `HttpOnly` + `Secure` + `SameSite=Strict` +
  `Path=/api/v1/auth`** (`Saas.Api.Common.RefreshCookie`), jamais dans le body JSON ni
  `localStorage` — posé/lu uniquement par les endpoints `/auth/login`, `/auth/refresh`,
  `/auth/logout`. Décision complète (attributs, pas d'antiforgery, exigence même-origine) :
  `docs/adr/0004`.
- **Hardening mots de passe** : PBKDF2-SHA512, `IterationCount = 210_000` (OWASP Password
  Storage Cheat Sheet) ; **lockout** après 5 tentatives échouées, verrouillage 5 minutes.
- **Connexion sans oracle de timing sur le hash** : pour un e-mail inconnu ou un compte verrouillé,
  `IdentityService.CheckCredentialsAsync` vérifie quand même un hash factice de même coût
  (`DummyPasswordCheck`) : le calcul PBKDF2, qui fait l'essentiel de la durée de la réponse, a
  lieu dans tous les cas. Un écart bien plus faible subsiste : un échec sur un compte existant
  enregistre aussi la tentative en base (`AccessFailedCount`).
  Sans organisation demandée, la connexion ouvre la plus ancienne adhésion.
- **Limite connue : l'inscription révèle un e-mail déjà pris.** Elle répond par l'erreur
  `DuplicateEmail` d'Identity, un compromis d'ergonomie courant. La fermer demanderait une
  inscription qui répond toujours de la même façon et prévient par e-mail le titulaire du compte.
- **Sessions** : chaque refresh fait tourner le refresh token. Présenter de nouveau un token déjà
  remplacé par une rotation est un signe de vol : toutes les sessions de l'utilisateur sont
  révoquées. Un token révoqué autrement (logout, réinitialisation, retrait d'une organisation)
  est seulement refusé, et un refresh refusé supprime le cookie. L'adhésion est vérifiée avant
  la rotation : un utilisateur retiré d'une organisation perd ses sessions sur celle-ci, sur
  chaque appareil, et garde celles de ses autres organisations. Une réinitialisation de mot de
  passe révoque toutes les sessions ouvertes.
- **Rôles d'organisation et policies** (`AuthorizationPolicies`) :

  | Policy | Claim exigé | Routes |
  |---|---|---|
  | `HasTenant` | `tenant` non vide | Produits |
  | `TenantAdmin` | `tenant_role` = `Owner` ou `Admin` | Invitations, suppression d'un produit |
  | `TenantOwner` | `tenant_role` = `Owner` | Changement de rôle d'un membre |

  Les tests d'intégration appliquent ces policies telles quelles : la factory de test remplace
  seulement le schéma d'authentification.
- Fallback policy : `RequireAuthenticatedUser` (toutes les routes sont auth par défaut).

### Résilience & observabilité

- **OpenTelemetry** : traces ASP.NET Core + HTTP client + métriques runtime. Exporte via OTLP si `OTEL_EXPORTER_OTLP_ENDPOINT` configuré.
- **Serilog** : structuré, lit la config JSON, écrit sur la console et transmet aux providers
  enregistrés, dont OpenTelemetry (export OTLP des logs). Un journal par requête, avec le statut
  final : une erreur de validation est un 400 journalisé en Information, sans log d'erreur
  (`ErrorLoggingTests`) ; une vraie erreur serveur garde son log d'erreur.
- **Rate limiter** : sliding window 100 req/min par user ou IP, fixed window 10 req/30s sur `/auth`.

---

## Couche Api

### Convention IEndpoint

```csharp
public sealed class ProductEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").RequireAuthorization("HasTenant");
        group.MapPost("/", CreateProduct).Produces<CreateProductResponse>(201);
        // ...
    }
    private static async Task<Created<CreateProductResponse>> CreateProduct(
        CreateProductCommand cmd, ISender sender, CancellationToken ct)
        => /* map → Send au Mediator */;
}
```

Enregistrement auto via `AddEndpoints()` / `MapEndpoints()` (scan de l'assembly Api). Les
endpoints reçoivent des contrats `Saas.Api.Contracts` (`XxxRequest`) et les traduisent en commandes :
le contrat HTTP ne dépend pas des types de l'Application.

### Contrat OpenAPI

Le build de l'API écrit le document OpenAPI dans `web-angular/openapi.json`, dont le front
génère ses types TypeScript (Hey API). La CI reconstruit le document et échoue s'il diffère du
fichier versionné. Le schéma Bearer n'est exigé que sur les opérations authentifiées.

### Cycle de vie d'une requête

```
HTTP POST /products
  → ProductEndpoints.CreateProduct (IEndpoint)
  → ISender.Send(CreateProductCommand)
    → LoggingBehavior
    → ValidationBehavior (CreateProductValidator)
    → CreateProductHandler
      → IProductRepository.Add(Product.Create(...))
      → IUnitOfWork.SaveChangesAsync()
        → DomainEventsInterceptor → ProductCreatedDomainEvent écrit dans OutboxMessages (même transaction)
  → TypedResults.Created(...)

Plus tard, hors requête :
OutboxPollingService (toutes les 5 s) → OutboxProcessor → IDomainEventHandler<ProductCreatedDomainEvent> (ProductCreatedHandler)
```

### Gestion des erreurs

- **Erreurs métier : `ErrorOr<T>`, jamais d'exception.** Chaque handler `ICommand<ErrorOr<T>>` /
  `IQuery<ErrorOr<T>>` retourne une erreur d'une classe `XxxErrors` centralisée (registre complet :
  `docs/error-codes.md`). Les endpoints convertissent via `result.ToHttpResult()` /
  `ToNoContentHttpResult()` / `ToCreatedHttpResult(...)` (`Saas.Api.Common.ErrorOrHttpExtensions`),
  qui mappe chaque `ErrorType` (`Validation`→400, `NotFound`→404, `Conflict`→409, `Forbidden`→403,
  `Unauthorized`→401, autre→500) sur un `ProblemDetails`.
- **Anti-énumération** : une ressource d'un autre tenant renvoie `NotFound`, jamais `Forbidden`
  (ne révèle pas son existence).
- **`GlobalExceptionHandler` reste responsable des exceptions légitimes restantes** (frontière
  forme/métier, `docs/adr/0003`) : `ValidationException` du pipeline FluentValidation ou des
  refus d'Identity liés à un champ, à l'inscription et à l'acceptation d'une invitation (→ 400
  `ProblemDetails` par champ), `DbUpdateException`/`DbUpdateConcurrencyException`
  côté infrastructure (→ 409, ex. contrainte d'unicité Postgres 23505 en défense en profondeur
  derrière une vérification applicative `ErrorOr`), toute autre exception non prévue → 500 (Serilog
  la logue).
- Vérifié par `ErrorOrConventionTests` (test d'archi) : tout `ICommand<T>`/`IQuery<T>` doit
  retourner `ErrorOr<>`, le `Domain` ne déclare aucune exception métier, et
  aucun handler ne lève de `ValidationException` hors des cas ci-dessus.
- Le registre `docs/error-codes.md` est comparé au code par `ErrorRegistryTests`.

---

## Front end

### Angular 22 (`web-angular/`)

- Signal-first (signals, `httpResource`, `input()`/`output()`).
- Standalone + zoneless. Control flow `@if`/`@for`.
- Angular Material seul.
- Types TypeScript générés par Hey API depuis `openapi.json` (`npm run api:gen`) ; les appels passent par `HttpClient`.

---

## Outillage

| Outil | Rôle |
|---|---|
| `mise` | Gestion des versions (.NET, Node) |
| `direnv` + `.envrc` | Variables d'env automatiques |
| `docker-compose` | Postgres + Adminer + Mailpit + Aspire dashboard |
| `lefthook` | Hooks git avant commit : format, lint, scan des secrets (gitleaks) |
| `Vogen` | Strongly-typed IDs + value objects mono-valeur |
| `openapi-ts` (Hey API) | Génération des types TypeScript de l'API |

---

## Error tracking (Sentry) — branchement optionnel

L'observabilité de base est assurée par **OpenTelemetry** (traces, métriques, logs via OTLP). Pour l'agrégation d'erreurs, les alertes et le suivi des releases, Sentry peut être branché **optionnellement** — il n'est pas câblé par défaut dans le starter.

### Backend (`Sentry.AspNetCore`)

```bash
dotnet add src/Api package Sentry.AspNetCore
```

Dans `Program.cs`, avant `builder.Build()` :

```csharp
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"]; // env var SENTRY__DSN
    o.TracesSampleRate = 0.2;
    o.Environment = builder.Environment.EnvironmentName;
});
```

Variable d'env à ajouter dans `.env` / secrets : `SENTRY__DSN=https://xxx@oyyy.ingest.sentry.io/zzz`.

### Front Angular (`@sentry/angular`)

```bash
npm install @sentry/angular
```

Dans `main.ts`, avant `bootstrapApplication` :
```typescript
import * as Sentry from '@sentry/angular';
Sentry.init({ dsn: '<DSN du projet Sentry front>' });
```

> Le DSN backend se passe par variable d'environnement ou secret, jamais dans le code. Le DSN front finit de toute façon dans le bundle JavaScript : l'injecter au build plutôt que de le committer. Ce branchement est volontairement absent du starter pour ne pas le lier à un compte Sentry.

---

## Robustesse (Outbox · Hangfire · HybridCache)

### Transactional Outbox

Les domain events ne sont pas dispatchés pendant `SaveChanges`. Le `DomainEventsInterceptor` les sérialise (STJ : `Type` = `AssemblyQualifiedName` + `Content` = JSON) en lignes `OutboxMessages` **pendant `SavingChanges`**, donc dans la **même transaction** que l'agrégat (atomicité : aucun event perdu sur crash, aucun event publié sans commit).

L'`OutboxPollingService` (`BackgroundService`) lance toutes les 5 s un lot de l'`OutboxProcessor`,
qui traite jusqu'à 20 messages, un par un :

- **Un message, une transaction** : le message est lu avec `FOR UPDATE SKIP LOCKED`, ses handlers
  sont appelés, puis son issue est validée. Plusieurs instances de l'API se partagent les messages
  sans traiter le même en même temps. Un échec ou un arrêt de l'application ne défait pas les
  messages déjà validés ; à l'arrêt, le message en cours reste à traiter, sans tentative comptée.
- **Handlers** : chaque `IDomainEventHandler<TEvent>` de l'Application (enregistré par scan
  d'assembly) reçoit l'event, dans son propre scope DI : il a son propre `DbContext` et valide ses
  écritures lui-même. Seuls les types qui implémentent `IDomainEvent` sont désérialisés.
- **Échecs** : le message est repris 1, 4, 16 puis 64 minutes plus tard (`NextAttemptOn`, erreur
  dans `Error`). Les handlers déjà passés sont notés dans `OutboxMessageConsumers` et ne sont pas
  rappelés : si l'e-mail de bienvenue est parti et qu'un autre handler échoue, l'e-mail ne repart
  pas. Si l'issue du message ne peut pas être enregistrée, la tentative est comptée à part. Après
  5 tentatives, le message est abandonné et journalisé en erreur (`DEPLOYMENT.md` explique comment
  le relancer).
- **Purge** : le job Hangfire quotidien `PurgeProcessedOutboxMessagesJob` supprime les messages
  traités depuis plus de 7 jours. Les messages abandonnés restent en base.
- **Garantie** : livraison « au moins une fois ». Un arrêt brutal entre un envoi externe et le
  commit fait rappeler ce handler ; un handler qui agit hors de la base doit le tolérer.

`OutboxTests` couvre le traitement, le délai avant une nouvelle tentative sans rappel des handlers
passés, l'abandon après 5 tentatives, l'issue impossible à enregistrer, l'arrêt de l'application en
cours de lot, le message verrouillé par une autre instance et la purge. Les tests d'intégration
retirent l'`OutboxPollingService` et lancent les lots eux-mêmes, avec une horloge qu'ils avancent.
`ConventionTests` vérifie que chaque event a un handler et survit à l'aller-retour JSON de l'outbox.

> Conséquence : les handlers de domain events (`ProductCreatedHandler`, etc.) s'exécutent **de façon asynchrone**, au lot suivant, et non pendant le `SaveChanges`.

### Hangfire (jobs persistés)

`Hangfire.AspNetCore` + `Hangfire.PostgreSql` — Hangfire crée ses **propres tables** Postgres au démarrage (pas de migration EF). API de config version-sensible : `UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connString))`.

- **Dashboard** : `/hangfire`, sécurisé par `HangfireDashboardAuthFilter`. Dev : loopback uniquement. Prod : claim `platform_role=PlatformAdmin` obligatoire.
- **Recurring jobs** (quotidiens, `ExecuteDeleteAsync`) : `PurgeExpiredRefreshTokensJob` (refresh tokens expirés) et `PurgeProcessedOutboxMessagesJob` (messages de l'outbox traités depuis plus de 7 jours).
- **Fire-and-forget** : `ProcessPasswordResetJob` et `ProcessInvitationEmailJob`, enfilés par la demande de réinitialisation et par l'invitation, pour que la réponse HTTP n'attende pas le SMTP.

> **Sécurité dashboard** : le dashboard est **secure-by-default** — le claim `platform_role` n'est émis par aucun flow de login standard. Le claim `tenant_role=Owner` ne suffit pas : le dashboard montre les jobs de tous les tenants. En prod, personne n'accède au dashboard tant qu'un opérateur infra n'est pas explicitement provisionné avec `platform_role=PlatformAdmin` (seed admin ou config IdP). Alternative : ne monter le dashboard que hors-production.

> Durcissement complémentaire : placer `/hangfire` derrière un reverse-proxy/VPN ou une auth interactive dédiée.

### HybridCache (.NET 10)

`AddHybridCache()` — **L1 en mémoire** par défaut. Le handler qui veut un cache l'utilise
explicitement : `GetProductByIdHandler` appelle `GetOrCreateAsync` avec une clé et un tag qui
contiennent le tenant (`ProductCache`), et `UpdateProductHandler`/`DeleteProductHandler`
invalident ce tag avec `RemoveByTagAsync`. L'invalidation est **logique**, pas physique : la doc
HybridCache précise que `RemoveByTagAsync` ignore toute entrée antérieure au point
d'invalidation plutôt que de la supprimer. Un produit absent est mis en cache comme absent : les
identifiants étant des UUID v7, aucun produit n'apparaît plus tard sous un identifiant demandé.

**Passer en distribué (L2 Redis)** : ajouter `Microsoft.Extensions.Caching.StackExchangeRedis` puis :
```csharp
services.AddStackExchangeRedisCache(o => o.Configuration = configuration.GetConnectionString("Redis"));
services.AddHybridCache(); // utilisera automatiquement Redis comme L2
```

---

## ADR

- `docs/adr/0001` — Strongly-typed IDs & value objects via Vogen (STJ-only + converter EF manuel)
- `docs/adr/0002` — Persistance CQRS (repos write par agrégat + Dapper read ; Ardalis.Specification écarté)
- `docs/adr/0003` — ErrorOr pour les erreurs métier (frontière avec les exceptions légitimes)
- `docs/adr/0004` — Refresh token en cookie HttpOnly (pas d'antiforgery ; front et API en même origine)
