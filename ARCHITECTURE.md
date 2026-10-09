# Architecture — Saas template

Template SaaS .NET 10 + Angular 22, organisé en monorepo.

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
`Domain` n'a aucune dépendance sur EF, ASP.NET ou autre framework. Vérifié par `DependencyRuleTests` (NetArchTest).

### Agrégats

| Agrégat | ID typé | Tenant-scoped | Interfaces |
|---|---|---|---|
| `Product` | `ProductId` | Oui (global query filter) | `IAuditable`, `ISoftDeletable`, `ITenantOwned` |
| `Organization` | `OrganizationId` | Non (cross-tenant) | `IAuditable` |

**Conventions** :
- Constructeur privé (EF navigation).
- Factory statique (`Product.Create(name, price)`) — valide les invariants, lève `ArgumentException` si violés.
- Setters privés — mutations uniquement via méthodes de comportement (`Rename`, `ChangePrice`, `AddMember`).
- Domain events : l'agrégat appelle `Raise(new XxxDomainEvent(...))`. Persistés en transaction par `DomainEventsInterceptor` puis dispatchés de façon fiable par l'`OutboxProcessor` (voir « Robustesse » plus bas).

### Value objects

| Type | Mécanisme | EF |
|---|---|---|
| `Money` (montant + devise) | `record` C# | Owned type EF |
| `Slug` | Vogen `[ValueObject<string>]` | `ValueConverter<Slug, string>` manuel |
| `ProductId`, `OrganizationId` | Vogen `[ValueObject<Guid>]` | `ValueConverter<XxxId, Guid>` manuel |

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
| `IUnitOfWork` | `SaveChangesAsync`, `ExecuteInTransactionAsync` |
| `IDbConnectionFactory` | Dapper — connexion pour les reads |
| `ICurrentUser` | UserId + TenantId du JWT courant |
| `ITenantProvider` | TenantId résolu pour EF |

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
| `DomainEventsInterceptor` | Collecte les events sur les agrégats et les dispatch via Mediator après `SaveChanges` |

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
- L'`AuditableEntityInterceptor` injecte `TenantId` sur les nouveaux agrégats tenant-scoped.

### Identité & Auth

- ASP.NET Core Identity (`ApplicationUser`, `IdentityRole<Guid>`) stocké en Postgres.
- JWT maison : `JwtTokenService` (access token 15min) + `RefreshTokenStore` (refresh token en base).
- **Refresh token transporté par cookie `HttpOnly` + `Secure` + `SameSite=Strict` +
  `Path=/api/v1/auth`** (`Saas.Api.Common.RefreshCookie`), jamais dans le body JSON ni
  `localStorage` — posé/lu uniquement par les endpoints `/auth/login`, `/auth/refresh`,
  `/auth/logout`. Décision complète (attributs, pas d'antiforgery, exigence même-origine) :
  `docs/adr/0004`.
- **Hardening mots de passe** : PBKDF2-SHA512, `IterationCount = 210_000` (OWASP Password
  Storage Cheat Sheet) ; **lockout** après 5 tentatives échouées, verrouillage 5 minutes.
- Policies : `"HasTenant"` (claim `tenant` non vide), `"TenantAdmin"` (claim `tenant_role = Owner`).
- Fallback policy : `RequireAuthenticatedUser` (toutes les routes sont auth par défaut).

### Résilience & observabilité

- **Résilience HTTP** : `Microsoft.Extensions.Http.Resilience` (disponible, à utiliser pour les HttpClient sortants).
- **OpenTelemetry** : traces ASP.NET Core + HTTP client + métriques runtime. Exporte via OTLP si `OTEL_EXPORTER_OTLP_ENDPOINT` configuré.
- **Serilog** : structuré, lit la config JSON, console en dev.
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

Enregistrement auto via `AddEndpoints()` / `MapEndpoints()` (scan de l'assembly Api).

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
        → DomainEventsInterceptor → dispatch ProductCreatedDomainEvent
  → TypedResults.Created(...)
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
  erreurs Identity field-tied (→ 400 `ProblemDetails` par champ), `DbUpdateException`/`DbUpdateConcurrencyException`
  côté infrastructure (→ 409, ex. contrainte d'unicité Postgres 23505 en défense en profondeur
  derrière une vérification applicative `ErrorOr`), toute autre exception non prévue → 500 (Serilog
  la logue).
- Vérifié par `ErrorOrConventionTests` (test d'archi) : tout `ICommand<T>`/`IQuery<T>` doit
  retourner `ErrorOr<>`, et aucune exception métier custom ne doit réapparaître dans `Domain`.

---

## Fronts

### Angular 22 (`web-angular/`)

- Signal-first (signals, `httpResource`, `input()`/`output()`).
- Standalone + zoneless. Control flow `@if`/`@for`.
- Angular Material seul.
- Client Hey API généré (`npm run api:gen`).

---

## Outillage

| Outil | Rôle |
|---|---|
| `mise` | Gestion des versions (.NET, Node) |
| `direnv` + `.envrc` | Variables d'env automatiques |
| `docker-compose` | Postgres + Adminer + Mailpit + Aspire dashboard |
| `lefthook` | Hooks git (lint/format avant commit) |
| `Vogen` | Strongly-typed IDs + value objects mono-valeur |
| `openapi-ts` (Hey API) | Génération des clients TypeScript |

---

---

## Error tracking (Sentry) — branchement optionnel

L'observabilité de base est assurée par **OpenTelemetry** (traces, métriques, logs via OTLP). Pour l'agrégation d'erreurs, les alertes et le suivi des releases, Sentry peut être branché **optionnellement** — il n'est pas câblé par défaut dans le template.

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

Dans `main.ts` :
```typescript
import * as Sentry from '@sentry/angular';
Sentry.init({ dsn: import.meta.env['VITE_SENTRY_DSN'] });
```

> **Per-SaaS uniquement** : ne pas committer le DSN dans le code. Utiliser les secrets GitHub Actions (prod) et les variables d'env locales (dev). Ce branchement est volontairement absent du template pour ne pas coupler tous les projets générés à un compte Sentry spécifique.

---

## Robustesse (Outbox · Hangfire · HybridCache)

### Transactional Outbox

Les domain events ne sont plus dispatchés directement après `SaveChanges`. Le `DomainEventsInterceptor` les sérialise (STJ : `Type` = `AssemblyQualifiedName` + `Content` = JSON) en lignes `OutboxMessages` **pendant `SavingChanges`**, donc dans la **même transaction** que l'agrégat (atomicité : aucun event perdu sur crash, aucun event publié sans commit). Un `OutboxProcessor : BackgroundService` poll les lignes non traitées toutes les 5 s, désérialise sur le type concret, publie via `IPublisher.Publish` dans un scope DI, puis renseigne `ProcessedOn`. Retry borné via `Attempts < 5` (l'erreur est stockée dans `Error`).

> Conséquence : les handlers de domain events (`ProductCreatedHandler`, etc.) s'exécutent désormais **de façon asynchrone** (au prochain tick du processor), plus de manière synchrone au `SaveChanges`.

### Hangfire (jobs persistés)

`Hangfire.AspNetCore` + `Hangfire.PostgreSql` — Hangfire crée ses **propres tables** Postgres au démarrage (pas de migration EF). API de config version-sensible : `UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connString))`.

- **Dashboard** : `/hangfire`, sécurisé par `HangfireDashboardAuthFilter`. Dev : loopback uniquement. Prod : claim `platform_role=PlatformAdmin` obligatoire.
- **Recurring job réel** : `PurgeExpiredRefreshTokensJob` (purge quotidienne des refresh tokens expirés/révoqués via `ExecuteDeleteAsync`).
- **Fire-and-forget** : `BackgroundJob.Enqueue<PurgeExpiredRefreshTokensJob>(j => j.RunAsync())`.

> **Sécurité dashboard** : le dashboard est **secure-by-default** — le claim `platform_role` n'est émis par aucun flow de login standard (ni par le claim `tenant_role=Owner`). Tout propriétaire de tenant aurait vu les jobs de TOUS les tenants s'il avait été utilisé. En prod, personne n'accède au dashboard tant qu'un opérateur infra n'est pas explicitement provisionné avec `platform_role=PlatformAdmin` (seed admin ou config IdP). Alternative : ne monter le dashboard que hors-production.

> Durcissement complémentaire : placer `/hangfire` derrière un reverse-proxy/VPN ou une auth interactive dédiée.

### HybridCache (.NET 10)

`AddHybridCache()` — **L1 en mémoire** par défaut. Cache **déclaratif opt-in** : décorer un
`record XxxQuery` de `[CachedQuery(seconds, Tag = "…")]` active le `CachingBehavior<,>` du
pipeline Mediator (enregistré après `ValidationBehavior`) — une query sans l'attribut traverse
le behavior sans écriture de cache. Réservé aux queries **tenant-scoped** : clé
(`q:{tenantId}:{Query.FullName}:{json}`) et tags (`t:{tenantId}` + `t:{tenantId}:{Tag}`) viennent
d'`ITenantProvider` (fail-closed). Seuls les succès sont cachés (jamais l'erreur d'un
`ErrorOr<T>`). Invalidation par write : `cache.RemoveByTagAsync($"t:{tenantId}:{tag}", ct)`
(`UpdateProductHandler`/`DeleteProductHandler`) — **logique**, pas physique (doc officielle
HybridCache : `RemoveByTagAsync` ignore toute entrée antérieure au point d'invalidation plutôt
que de la supprimer). Migré : `GetProductByIdQuery` (`[CachedQuery(60, Tag = "products")]`).

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
- `docs/adr/0004` — Refresh token en cookie HttpOnly (pas d'antiforgery ; fronts même-origine)
