# Saas — template monorepo

Template SaaS de production : **.NET 10** (Clean Architecture + DDD/CQRS) + **Angular 22** + **React 19** + **PostgreSQL 17**.

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
# → http://localhost:5001  |  OpenAPI: http://localhost:5001/openapi/v1.json

# 5. Front Angular (dans un terminal séparé)
cd web-angular && npm install && npm start
# → http://localhost:4200

# 6. Front React (dans un terminal séparé)
cd web-react && npm install && npm run dev
# → http://localhost:5173
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
| `cd web-react && npm run fix` | Lint + format React |
| `cd web-react && npm run check` | Gate CI React |
| `./.claude/hooks/check.sh` | Gate complète (build + tests + fronts) |

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
| Front React | React 19, Vite, TanStack Router + Query, shadcn/Tailwind v4, Hey API |
| Outillage | mise, direnv, docker-compose, lefthook, Vogen |

## Documentation

| Fichier | Contenu |
|---|---|
| `ARCHITECTURE.md` | Architecture complète (couches, DDD, CQRS, cycle de vie requête) |
| `CONTRIBUTING.md` | Workflow, skills, conventions, tests |
| `docs/adr/0001` | Strongly-typed IDs & value objects (Vogen STJ-only + converter EF) |
| `docs/adr/0002` | Persistance CQRS (repos write + Dapper read) |

## Travailler avec un agent Claude Code

Ce repo est outillé pour Claude Code :

- **`CLAUDE.md`** (racine + par zone) : carte du monorepo et commandes clés.
- **`.claude/rules/`** : conventions DDD/Angular/React, chargées automatiquement selon les fichiers touchés.
- **`/check`** (commande Claude Code) : lance `./.claude/hooks/check.sh` — gate complète.
- **Skills** : `/add-use-case`, `/add-aggregate`, `/add-typed-id`, `/add-migration`, `/sync-api-client`.
- **Stop hook** : gate rapide automatique (build + format-verify) à chaque fin de tour.
- **`.mcp.json`** : MCP server Postgres en lecture (`@bytebase/dbhub`, requiert `DATABASE_URL`).
- **`REVIEW.md`** : calibrage de la review automatique Claude.

```bash
# Ouvrir Claude Code sur ce projet
claude
```

## Review CI (optionnelle)

Pour activer la review automatique Claude sur les PR :
1. Installer l'app GitHub Claude Code sur le repo.
2. Commenter `@claude review` sur une PR, ou configurer `.github/workflows/claude-review.yml`.
3. `REVIEW.md` est déjà calibré (priorités, skips, must-checks DDD).

**Désactivée par défaut** (facturée à l'usage).

## Développement local — ports

| Service | URL |
|---|---|
| API .NET | http://localhost:5001 |
| Angular | http://localhost:4200 |
| React | http://localhost:5173 |
| Adminer (DB) | http://localhost:8080 |
| Mailpit (SMTP) | http://localhost:8025 |
| Aspire dashboard | http://localhost:18888 |
