# Architecture — Saas template

Template SaaS .NET 10 + Angular 22 + React 19, organisé en monorepo.

---

## Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────────┐
│  web-angular/   Angular 22 (signal-first, Material, Hey API)    │
│  web-react/     React 19 (Vite, TanStack, shadcn, Hey API)      │
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
- Domain events : l'agrégat appelle `Raise(new XxxDomainEvent(...))`. Dispatch automatique après `SaveChanges` via `DomainEventsInterceptor`.

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

- `TenantId` extrait du JWT claim `"tenant"` par `TenantProvider`.
- `HasQueryFilter(p => p.TenantId == tenant.TenantId && !p.IsDeleted)` sur `Product`.
- L'`AuditableEntityInterceptor` injecte `TenantId` sur les nouveaux agrégats tenant-scoped.

### Identité & Auth

- ASP.NET Core Identity (`ApplicationUser`, `IdentityRole<Guid>`) stocké en Postgres.
- JWT maison : `JwtTokenService` (access token 15min) + `RefreshTokenStore` (refresh token en base).
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

- `GlobalExceptionHandler` convertit `ValidationException` → 400 `ProblemDetails`, `NotFoundException` → 404.
- Toutes les autres exceptions → 500 (Serilog les logue).

---

## Fronts

### Angular 22 (`web-angular/`)

- Signal-first (signals, `httpResource`, `input()`/`output()`).
- Standalone + zoneless. Control flow `@if`/`@for`.
- Angular Material seul.
- Client Hey API généré (`npm run api:gen`).

### React 19 (`web-react/`)

- TanStack Router (file-based) + TanStack Query.
- Hooks générés Hey API (`useQuery(getProductsOptions())`).
- Zustand (auth), React Hook Form + Zod (formulaires).
- shadcn + Tailwind v4.
- Client Hey API généré (`npm run gen:api`).

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

## ADR

- `docs/adr/0001` — Strongly-typed IDs & value objects via Vogen (STJ-only + converter EF manuel)
- `docs/adr/0002` — Persistance CQRS (repos write par agrégat + Dapper read ; Ardalis.Specification écarté)
