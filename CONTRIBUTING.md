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

**`dotnet tool restore` est requis une fois** (ou après tout `pull` qui touche
`.config/dotnet-tools.json`) : installe les outils .NET locaux pinnés du manifest
(`dotnet-ef`, `dotnet-stryker`, `dotnet-CycloneDX`). Sans ça, `dotnet ef migrations add`,
`dotnet stryker` ou une génération de SBOM en local échouent (outil introuvable).

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
curl http://localhost:5123/openapi/v1.json -o web-angular/openapi.json
curl http://localhost:5123/openapi/v1.json -o web-react/openapi.json
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

## CI/CD — vue d'ensemble des workflows

| Workflow | Déclencheur | Rôle | Si ça casse |
|---|---|---|---|
| `ci.yml` | Push/PR/`merge_group` sur `main` | Build + tests (unit/intégration/archi) + gate lint/format/typecheck des 2 fronts + couverture backend (coverlet → summary + artifact, **non bloquante** — cf. § Couverture) | Lire le log du job en échec (build/tests/front) ; une couverture qui baisse n'échoue jamais le job, c'est un signal |
| `smoke.yml` | Push `main` + PR touchant le backend (`src/`, `Directory.Packages.props`, `scripts/smoke.sh`, `docker-compose.yml`) + manuel | Bout-en-bout réel (Postgres + Mailpit + API + Hangfire) via `scripts/smoke.sh` | Reproduire en local : `docker compose up -d db mailpit && dotnet run --project src/Api` puis `bash scripts/smoke.sh` |
| `deps-audit.yml` | PR touchant les manifests de deps (`Directory.Packages.props`, `packages.lock.json`, `package.json`/`package-lock.json`) + cron hebdo (lundi 05h UTC) + manuel | Bloque sur CVE High/Critical — `dotnet-audit` (NuGet, direct + transitif) et `npm-audit` (par front, prod deps seulement) | Corriger (bump/pin) ou documenter une exception datée — voir § Procédure d'exception CVE ci-dessous |
| `zizmor.yml` | Push/PR touchant `.github/workflows/**` | Lint de sécurité des workflows GitHub Actions (permissions, injections, cache-poisoning…) → SARIF dans l'onglet Security | Reproduire en local : `uvx zizmor .github/workflows/` ; corriger le finding (permissions minimales, pin SHA, échappement d'input) |
| `stryker.yml` | Cron hebdo + manuel | Mutation testing (Application + Domain) — **non bloquant actuellement** | Voir § Mutation testing ci-dessous (incompatibilité xunit.v3/MTP en cours) |
| `release.yml` | Tag `v*` + manuel | Build + push image GHCR, attestation de provenance (SLSA build L2), SBOM (CycloneDX backend + npm natif par front) | Voir `DEPLOYMENT.md` § Release — image, SBOM, attestation |
| `generator.yml` | Push/PR touchant `tools/new-saas`, `scripts/test-generator.sh` ou `.github/**` | Génère les 2 variantes du template (Angular/React) et vérifie l'absence de résidu (nom du template, front retiré, ports) + build | Reproduire en local : `bash scripts/test-generator.sh` (quelques minutes, build .NET compris) |
| `codeql.yml` / `security.yml` | Push/PR (préexistants) | SAST (CodeQL C#/TS) + secret scanning (gitleaks) | Voir l'onglet Security du repo |

### Renovate — mises à jour de dépendances

`renovate.json5` remplace Dependabot (`config:best-practices`, cooldown `minimumReleaseAge: 7 days`,
groupage par écosystème, `@hey-api/openapi-ts` jamais groupé — pré-1.0, minors potentiellement
breaking). **Action requise une seule fois côté utilisateur** : installer l'app GitHub
*Mend Renovate* sur le dépôt (https://github.com/apps/renovate) — le fichier de config seul ne
déclenche rien tant que l'app n'est pas installée sur le repo.

### Procédure d'exception CVE

Documentée intégralement dans `security/README.md` (§ Procédure d'exception) : ouvrir un ticket
de suivi, documenter dans `security/deps-review-*.md` (copier `deps-review-TEMPLATE.md`), ne
jamais désactiver `deps-audit.yml` pour contourner le blocage. `security/README.md` est écrit
pour être lisible par un auditeur externe sans contexte préalable.

### Couverture de code

`ci.yml` collecte la couverture backend (coverlet, format Cobertura) à chaque run, l'affiche en
summary et la publie en artifact (`coverage-backend`) — **volontairement non bloquante** : la
qualité réelle des tests est gatée par Stryker (mutation testing), pas par un seuil de lignes
couvertes (un seuil de couverture se contourne trivialement avec des tests sans assertion).

---

## Mutation testing (Stryker.NET — câblé, mesure non fiable actuellement)

Le mutation testing est censé mesurer la **qualité réelle des tests** en introduisant des
mutations dans le code et en vérifiant si les tests les détectent. L'outillage est en
place (workflow hebdo `.github/workflows/stryker.yml`, deux configs
`stryker-config.json` / `stryker-config-domain.json`, manifest `.config/dotnet-tools.json`)
mais **ne produit aujourd'hui aucune mesure fiable** sur cette stack.

### État constaté : incompatibilité Stryker.NET 4.16 ↔ xunit.v3/MTP

Deux runs complets (2026-07-13) ont démontré que l'intégration Stryker.NET 4.16 avec
xunit.v3 / Microsoft Testing Platform (MTP) est cassée :

- **Run Application** (12:09) : "score" affiché 97.84 %, mais **0 Killed / 226 Timeout**
  — les mutants tués remontent comme des timeouts génériques plutôt que comme des kills.
  Le chiffre est un artefact de l'intégration, pas un vrai signal de qualité.
- **Run Domain** (12:41, relancé 12:45) : **0.00 %** — **124/124 mutants SURVIVED**,
  0 killed, 0 timeout. Les tests passent contre chaque mutant : exécution à vide
  (artefact), pas un score réel de 0.
- Suivi upstream : [stryker-mutator/stryker-net#3117](https://github.com/stryker-mutator/stryker-net/issues/3117)
  et [#3629](https://github.com/stryker-mutator/stryker-net/issues/3629) (support MTP incomplet).

**Conséquence** : le job `stryker.yml` est marqué `continue-on-error: true` au niveau
job — il tourne chaque semaine (et sur `workflow_dispatch`) et publie ses rapports en
artifacts, mais **ne bloque rien** tant que l'intégration reste cassée. Les seuils
`break: 60` restent configurés comme cible pour le jour où la mesure redeviendra fiable ;
ils ne sont pas appliqués de façon fiable aujourd'hui.

### Signal à surveiller

Un futur run hebdomadaire avec des compteurs **`Killed` non nuls** dans le rapport
(au lieu de tout-Timeout ou tout-Survived) signale que le support MTP est arrivé côté
stryker-net. Dans ce cas : retirer le `continue-on-error: true` du job, et réévaluer les
seuils `break` à la lumière du vrai score. Renovate ouvrira les PRs de montée de version
de `dotnet-stryker` — chaque montée est l'occasion de relancer les deux configs et de
vérifier si `Killed` redevient non nul.

### Installation (locale, une seule fois)

```bash
dotnet tool install -g dotnet-stryker
# ou en tool local dans le projet :
dotnet tool install dotnet-stryker
```

### Lancer une analyse

```bash
# Depuis la racine du projet
dotnet stryker -f stryker-config.json          # Application
dotnet stryker -f stryker-config-domain.json   # Domain
```

Le rapport HTML s'ouvre dans `StrykerOutput/**/reports/mutation-report.html`.

### Configuration

Voir `stryker-config.json` (Application) et `stryker-config-domain.json` (Domain) à la
racine. Chaque config exclut les migrations et fichiers générés (`.g.cs`), coverage
analysis désactivée (`"coverage-analysis": "off"`, requis avec xunit.v3/MTP).

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
dotnet run --project src/Api      # API sur http://localhost:5123
cd web-angular && npm start        # Angular sur http://localhost:4200
cd web-react && npm run dev        # React sur http://localhost:5173

# Format & lint
dotnet format                      # .NET
cd web-angular && npm run fix      # Angular
cd web-react && npm run fix        # React
```
