# Saas — template monorepo

Template SaaS de production : **.NET 10** (Clean Architecture + DDD/CQRS) + **Angular 22** + **React 19** + **PostgreSQL 17**.

## Générer un nouveau SaaS

Le script `~/.local/bin/new-saas` copie ce template et le tokenise pour créer un nouveau projet.

```bash
new-saas <nom> [--front=angular|react] [--public|--private] [--no-install] [--no-git] [--no-repo]

# Exemples
new-saas facturation                  # Angular + repo GitHub privé + push
new-saas crm --front=react --public   # React/Vite + repo GitHub public
new-saas demo --no-repo               # pas de création de repo distant
```

### Ce que fait `new-saas`

1. **Copie** le template `~/dev/.templates/saas/` → `~/dev/<nom>/` (rsync, sans `bin/`, `obj/`, `node_modules/`, `docs/superpowers/`, `.superpowers/` (scratch de planification), etc.)
2. **Tokenise l'identifiant** `Saas` → `<Pascal>` (namespaces, `.csproj`, `.slnx`, contenu des `.cs`, `.json`, `.ts`…) et renomme les fichiers.
3. **Tokenise le nom lowercase** `saas` → `<nom>` (docker-compose, `.env.example`, appsettings, connection strings, labels JWT…).
4. **Alloue un bloc de 10 ports** depuis le registre `~/dev/.saas-ports.tsv` (base depuis 5400, incrément de 10) et remplace les ports hardcodés dans launchSettings, proxy.conf, vite.config (dev server + proxy API), appsettings, docker-compose, smoke.sh, `.env.example`.
5. **Retire le front non choisi** (`web-react/` ou `web-angular/`) et nettoie les références mortes : jobs CI (`ci.yml`, `deps-audit.yml`, `release.yml`), hooks lefthook, règle `.claude/rules/`, CLAUDE.md, `.claude/agents/doc-updater.md`, `security/README.md`, `security/deps-review-TEMPLATE.md`.
6. **`git init` + commit initial** + `gh repo create` (si `gh` est authentifié et `--no-repo` non spécifié).
7. **Met à jour le registre** `~/dev/.saas-ports.tsv` avec le nom et la base de ports allouée.

### Post-scaffold

```bash
saas <nom>                              # cd ~/dev/<nom>
direnv allow                            # activer le .envrc (1ère fois)
docker network create devnet 2>/dev/null || true
docker compose up -d                    # Postgres + Adminer + Mailpit
mise exec -- dotnet run --project src/Api

# Front Angular
cd web-angular && npm install && npm start

# Front React
cd web-react && npm install && npm run dev
```

Activer les plugins LSP (scope utilisateur) : `claude mcp add csharp-ls --scope user -- csharp-ls`.

---

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
| `DEPLOYMENT.md` | Déploiement (image Docker, secrets prod, migrations bundle, health probes, scaling) |
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

## Licence

Par défaut, ce template utilise une **licence propriétaire** (`LICENSE`) avec les placeholders `__YEAR__` et `__OWNER__`.
Remplacez ces valeurs après le scaffold. Si vous souhaitez open-sourcer le projet, remplacez `LICENSE` par MIT, Apache-2.0 ou autre licence adaptée.

## Développement local — ports

| Service | URL |
|---|---|
| API .NET | http://localhost:5123 |
| Angular | http://localhost:4200 |
| React | http://localhost:5173 |
| Adminer (DB) | http://localhost:8080 |
| Mailpit (SMTP) | http://localhost:8025 |
| Aspire dashboard | http://localhost:18888 |
