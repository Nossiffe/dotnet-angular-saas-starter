# CONTRIBUTING.md — Workflow de développement

## Mode de travail recommandé (avec ou sans agent)

### Cycle explore → plan → code → commit

1. **Explore** : lire `ARCHITECTURE.md`, les ADR (`docs/adr/`), et le code existant avant de toucher quoi que ce soit.
2. **Plan** : décrire précisément ce qui va changer (use-case, agrégat, migration, endpoint). En mode agent, utiliser le **plan mode** de Claude Code (`/plan`) pour valider avant d'écrire.
3. **Code** : implémenter en suivant les conventions décrites dans `CLAUDE.md` et `.claude/rules/`.
4. **Commit** : un commit par changement cohérent, message Conventional Commits.

### Sessions longues avec un agent

Utiliser `/goal` pour définir l'objectif de la session :
```
/goal Tous les tests passent au vert après ajout de l'agrégat Invoice
```
L'agent garde l'objectif en tête et ne se déclare pas fini tant que la gate n'est pas verte.

---

## Boucle de vérification (ne jamais s'en passer)

```bash
# Gate rapide — auto-lancée par le Stop hook à chaque fin de tour
./.claude/hooks/check-fast.sh   # dotnet build + format-verify

# Gate complète — avant tout commit/PR important
./.claude/hooks/check.sh        # build + tests + check des 2 fronts
# ou taper /check dans Claude Code
```

**Docker est requis** pour les tests d'intégration (Testcontainers).

---

## Ajouter une feature

### Use-case CQRS (skill `/add-use-case`)

1. Créer `Application/<Feature>/<UseCase>/` avec `Command/Query` + `Handler` + `Validator`.
2. Si nouvelle route : ajouter dans `Api/Endpoints/<Feature>Endpoints.cs` (`IEndpoint`).
3. Tests : unit (validator) + intégration (endpoint).

Détail : `.claude/skills/add-use-case/SKILL.md`

### Agrégat DDD (skill `/add-aggregate`)

1. Typed ID Vogen (`Domain`) + converter EF (`Infrastructure/Converters/`) + `ConfigureConventions`.
2. Agrégat (`Domain`) : factory statique, setters privés, méthodes de comportement.
3. Config EF + `DbSet internal` + interface repo (Application) + implémentation (Infrastructure).
4. Migration EF.
5. Tests unitaires (agrégat) + intégration.

Détail : `.claude/skills/add-aggregate/SKILL.md` + `docs/adr/0001` + `docs/adr/0002`

### Strongly-typed ID (skill `/add-typed-id`)

Recette Vogen en 3 étapes : `Domain` STJ-only → converter EF `Infrastructure` → `ConfigureConventions`.
Détail : `.claude/skills/add-typed-id/SKILL.md` + `docs/adr/0001`

### Migration EF (skill `/add-migration`)

```bash
dotnet ef migrations add <NomMigration> --project src/Infrastructure --startup-project src/Api
```
Toujours relire le fichier généré avant de commiter. Détail : `.claude/skills/add-migration/SKILL.md`

### Regénérer les clients API (skill `/sync-api-client`)

```bash
curl http://localhost:5001/openapi/v1.json -o web-angular/openapi.json
curl http://localhost:5001/openapi/v1.json -o web-react/openapi.json
cd web-angular && npm run api:gen
cd web-react && npm run gen:api
```
Détail : `.claude/skills/sync-api-client/SKILL.md`

---

## Conventions de commit (Conventional Commits)

| Préfixe | Utilisation |
|---|---|
| `feat:` | Nouvelle fonctionnalité |
| `fix:` | Correction de bug |
| `refactor:` | Refactoring sans changement de comportement |
| `test:` | Ajout/modification de tests |
| `docs:` | Documentation uniquement |
| `chore:` | Outillage, config, dépendances |
| `build:` | Système de build, CI |
| `ci:` | Pipelines CI/CD |

Exemples :
```
feat(products): add UpdateProduct use-case
fix(auth): fix refresh token expiry check
chore(db): migration AddInvoiceAggregate
docs(adr): ADR-0003 slug uniqueness constraint
```

---

## Tests

- **Unit** (`src/tests/UnitTests/`) : agrégats, validators, archi. Rapides, sans Docker.
- **Intégration** (`src/tests/IntegrationTests/`) : endpoints via `WebApplicationFactory` + Testcontainers (Docker requis). Couvrir : cas nominal, 400, 401, 403, isolation tenant.
- **Archi** (`DependencyRuleTests`) : NetArchTest vérifie la règle de dépendance Clean Arch.

```bash
dotnet test                    # tous
dotnet test --filter "Unit"    # unit seulement (sans Docker)
```

---

## Review Claude en CI (optionnelle)

Le fichier `REVIEW.md` à la racine calibre la review automatique (priorités, skips, must-checks).

Pour activer :
1. Installer l'app GitHub Claude Code sur le repo.
2. Commenter `@claude review` sur une PR, ou configurer `.github/workflows/claude-review.yml`.
3. La review est **désactivée par défaut** (facturée à l'usage). Activer uniquement si voulu.

---

## Outillage développeur

```bash
# Démarrage complet
docker network create devnet 2>/dev/null || true
docker compose up -d
dotnet run --project src/Api      # API sur http://localhost:5001
cd web-angular && npm start        # Angular sur http://localhost:4200
cd web-react && npm run dev        # React sur http://localhost:5173

# Format & lint
dotnet format                      # .NET
cd web-angular && npm run fix      # Angular
cd web-react && npm run fix        # React
```
