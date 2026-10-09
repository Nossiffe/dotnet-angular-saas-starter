# Saas — template monorepo

Template SaaS de production : **.NET 10** (Clean Architecture + DDD/CQRS) + **Angular 22** + **PostgreSQL 17**.

## Quickstart

```bash
# 1. Réseau Docker partagé (une seule fois par machine)
docker network create devnet 2>/dev/null || true

# 2. Copier les variables d'environnement
cp .env.example .env

# 3. Démarrer la stack
docker compose up -d

# 4. Démarrer l'API
dotnet run --project src/Api
# → http://localhost:5123  |  OpenAPI: http://localhost:5123/openapi/v1.json

# 5. Front Angular (dans un terminal séparé)
cd web-angular && npm install && npm start
# → http://localhost:4200

```

## Commandes clés

| Commande | Description |
|---|---|
| `dotnet build` | Compile la solution |
| `dotnet test` | Tests (unit + intégration + archi) — Docker requis |
| `dotnet format` | Formate le code C# |
| `dotnet ef migrations add <Nom> --project src/Infrastructure --startup-project src/Api` | Nouvelle migration EF |
| `cd web-angular && npm run fix` | Lint + format Angular |
| `cd web-angular && npm run check` | Gate CI Angular |
| `./scripts/check.sh` | Gate complète (build + tests + front) |

## Stack

| Couche | Technologie |
|---|---|
| Backend | .NET 10, Clean Architecture + DDD (agrégats riches, Vogen) |
| CQRS | Mediator (Mediator.NET), FluentValidation |
| Persistance | EF Core 10 (write) + Dapper (read), Npgsql, Postgres 17 |
| Auth | ASP.NET Core Identity + JWT maison (access + refresh) |
| Multi-tenant | Global query filter EF + claim JWT `tenant` |
| Résilience | EnableRetryOnFailure + Http.Resilience |
| Observabilité | Serilog + OpenTelemetry (OTLP) |
| Front Angular | Angular 22, signal-first, Material, Hey API |
| Outillage | mise, direnv, docker-compose, lefthook, Vogen |

## Documentation

| Fichier | Contenu |
|---|---|
| `ARCHITECTURE.md` | Architecture complète (couches, DDD, CQRS, cycle de vie requête) |
| `CONTRIBUTING.md` | Workflow, skills, conventions, tests |
| `DEPLOYMENT.md` | Déploiement (image Docker, secrets prod, migrations bundle, health probes, scaling) |
| `docs/adr/0001` | Strongly-typed IDs & value objects (Vogen STJ-only + converter EF) |
| `docs/adr/0002` | Persistance CQRS (repos write + Dapper read) |

## Licence

Par défaut, ce template utilise une **licence propriétaire** (`LICENSE`) avec les placeholders `__YEAR__` et `__OWNER__`.
Remplacez ces valeurs après le scaffold. Si vous souhaitez open-sourcer le projet, remplacez `LICENSE` par MIT, Apache-2.0 ou autre licence adaptée.

## Développement local — ports

| Service | URL |
|---|---|
| API .NET | http://localhost:5123 |
| Angular | http://localhost:4200 |
| Adminer (DB) | http://localhost:8080 |
| Mailpit (SMTP) | http://localhost:8025 |
| Aspire dashboard | http://localhost:18888 |
