# CONTRIBUTING.md — Workflow de développement

## Mode de travail recommandé

### Démarche

1. Lire `ARCHITECTURE.md`, les ADR (`docs/adr/`) et le code concerné.
2. Décrire le changement dans l'issue ou la PR : use-case, agrégat, migration, endpoint.
3. Implémenter selon les conventions de `ARCHITECTURE.md`, des ADR et de ce guide.
4. Un commit par changement cohérent, message Conventional Commits.

---

## Vérifications locales

```bash
# Gate rapide
./scripts/check-fast.sh   # dotnet build + format-verify

# Gate complète — avant tout commit/PR important
./scripts/check.sh        # build + tests + check du front
```

**Docker est requis** pour les tests d'intégration (Testcontainers).

**`dotnet tool restore` est requis une fois** (ou après tout `pull` qui touche
`.config/dotnet-tools.json`) : installe les outils .NET locaux pinnés du manifest
(`dotnet-ef`, `dotnet-stryker`, `dotnet-CycloneDX`). Sans ça, `dotnet ef migrations add`,
`dotnet stryker` ou une génération de SBOM en local échouent (outil introuvable).

---

## Ajouter une feature

### Use-case CQRS

1. Créer `Application/<Feature>/<UseCase>/` avec `Command/Query` + `Handler` + `Validator`.
2. Si nouvelle route : ajouter dans `Api/Endpoints/<Feature>Endpoints.cs` (`IEndpoint`).
3. Tests : unit (validator) + intégration (endpoint).

### Agrégat DDD

1. Typed ID Vogen (`Domain`) + converter EF (`Infrastructure/Persistence/Converters/`) + `ConfigureConventions`.
2. Agrégat (`Domain`) : factory statique, setters privés, méthodes de comportement.
3. Config EF + `DbSet internal` + interface repo (Application) + implémentation (Infrastructure).
4. Migration EF.
5. Tests unitaires (agrégat) + intégration.

Détail : `docs/adr/0001` + `docs/adr/0002`

### Strongly-typed ID

Recette Vogen en 3 étapes : `Domain` STJ-only → converter EF `Infrastructure` → `ConfigureConventions`.
Détail : `docs/adr/0001`

### Migration EF

```bash
dotnet ef migrations add <NomMigration> --project src/Infrastructure --startup-project src/Api
```
Toujours relire le fichier généré avant de commiter.

### Modèles EF compilés (`dotnet ef dbcontext optimize`) — non activé par défaut

**Qu'est-ce que c'est** : `dotnet ef dbcontext optimize` génère une version **compilée** du
modèle EF Core (classes C# générées, `RuntimeModel`) au lieu de le construire par réflexion au
premier accès au `DbContext`. Gain : temps de démarrage (le modèle n'est plus construit au
runtime) — significatif seulement sur de **gros modèles** (centaines à milliers d'entity
types/relations selon la doc officielle EF Core, § « Compiled models »).

```bash
dotnet ef dbcontext optimize --project src/Infrastructure --startup-project src/Api
```

**Quand l'utiliser** : modèle EF volumineux où le temps de démarrage devient sensible, ou
publication **Native AOT** (`--nativeaot --precompile-queries`, requis pour AOT si le projet
contient des requêtes).

**Pourquoi ce n'est pas activé par défaut** :
- **Incompatible avec les global query filters** — limitation documentée par Microsoft
  (« Advanced Performance Topics » § Compiled models, « Limitations ») : *"Global query filters
  are not supported"*. Or l'isolation multi-tenant de ce starter repose entièrement sur les
  named query filters EF (`"TenantFilter"`/`"SoftDeleteFilter"`)
  — activer un modèle compilé casserait cette garantie « par construction ». C'est la raison
  bloquante, avant même la taille du modèle actuel.
- Fichiers générés à maintenir : le modèle compilé doit être **régénéré manuellement** à chaque
  changement de modèle/configuration (pas de détection automatique sans le package MSBuild
  `Microsoft.EntityFrameworkCore.Tasks`) — un pas de process supplémentaire pour un gain non
  mesuré ici (modèle actuel : quelques agrégats, très loin des « centaines/milliers » où le gain
  devient perceptible). YAGNI à cette taille de modèle.

### Régénérer le contrat OpenAPI et les types TypeScript

Le build de l'API écrit le contrat dans `web-angular/openapi.json`, le fichier versionné dont le
front génère ses types (`src/app/api/generated/`, non versionné). Après un changement d'endpoint :

```bash
dotnet build src/Api                 # met à jour web-angular/openapi.json
cd web-angular && npm run api:gen    # régénère les types TypeScript
```

Committer `web-angular/openapi.json` avec le changement : la CI échoue si le contrat versionné
diffère de celui que produit le build.

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

## Release & CHANGELOG

`CHANGELOG.md` suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) : une
section `## [Unreleased]` accumule les changements en cours, avec toujours une rubrique
**Migrations DB** (même vide : « aucune ») — pour ne jamais oublier un `database update`
au déploiement (cf. `DEPLOYMENT.md` § Migration Bundle).

### Au tag `v*`

1. Basculer `## [Unreleased]` vers une section versionnée (`## [x.y.z] - AAAA-MM-JJ`),
   puis recréer une section `## [Unreleased]` vide au-dessus pour la suite.
2. Remplir la rubrique **Migrations DB** de la nouvelle section à partir des migrations
   EF ajoutées depuis le tag précédent :
   ```bash
   git diff <tag-précédent>..HEAD --stat -- '**/Migrations/'
   ```
   Si la commande ne retourne rien : « aucune ».
3. Lier cette rubrique au **Migration Bundle** (`DEPLOYMENT.md` § Production — Migration
   Bundle) — c'est ce bundle qui applique ces migrations en production, une seule fois par
   release, avant le déploiement de la nouvelle version.

`release.yml` (déclenché par le tag) gère le build/push image + SBOM + attestation, puis
crée la GitHub Release du tag avec les SBOM en pièces jointes (cf. tableau CI/CD ci-dessous).
Il lit la section du `CHANGELOG.md` pour les notes de la release (`scripts/release-notes.sh`),
sans le modifier, et s'arrête avant de publier quoi que ce soit si elle manque ou n'a pas de
rubrique **Migrations DB** remplie (« aucune » au moins) : le `CHANGELOG.md` est mis à jour à la main, dans une PR fusionnée
avant de poser le tag.

---

## Tests

- **Unit** (`src/tests/UnitTests/`) : agrégats, validateurs, handlers sans accès direct à la base, archi, registre des erreurs. Rapides, sans Docker.
- **Intégration** (`src/tests/IntegrationTests/`) : endpoints via `WebApplicationFactory` + Testcontainers (Docker requis). Couvrir : cas nominal, 400, 401, 403, isolation tenant.
- **Archi** (`DependencyRuleTests`) : NetArchTest vérifie la règle de dépendance Clean Arch.
- **Front** (`web-angular/`) : tests unitaires Vitest (`npm test`) et Playwright sur les pages
  de connexion, d'acceptation d'invitation, Produits et Organisation, API simulée, avec contrôle
  d'accessibilité axe (`npm run e2e`).

```bash
dotnet test                                                  # tous
dotnet test --project src/tests/UnitTests/Saas.UnitTests.csproj  # unit seulement (sans Docker)
```

> ⚠️ **Runner MTP** : `dotnet test` transmet les options qu'il ne connaît pas aux
> exécutables de test ; une option VSTest historique (`--nologo`, `--collect`…) est
> rejetée par MTP → chaque projet imprime son aide et le run se termine en « Zéro
> tests exécutés » (exit 5). En cas d'exit 5 inattendu, retirer les options avant de
> soupçonner les tests. Exception : depuis xunit.v3 4, `--filter` (syntaxe VSTest) est
> accepté, par exemple `--filter "FullyQualifiedName~RateLimitingTests"`.

---

## CI/CD — vue d'ensemble des workflows

| Workflow | Déclencheur | Rôle | Si ça casse |
|---|---|---|---|
| `ci.yml` | Push/PR/`merge_group` sur `main` | Restore NuGet verrouillé (contrôle que chaque projet l'est) + build + tests (unit/intégration/archi) + front Angular (lint/format/typecheck, tests unitaires, build, Playwright + axe sur les pages de connexion, d'acceptation d'invitation, Produits et Organisation) + vérification des chemins cités dans la doc (`scripts/check-docs.sh`) + couverture backend (Microsoft.CodeCoverage MTP, résumé ReportGenerator + artifact, **non bloquante** — cf. § Couverture) | Lire le log du job en échec (build/tests/front) ; NU1004 au restore : un `packages.lock.json` périmé, à régénérer par `dotnet restore` en local puis committer ; une couverture qui baisse n'échoue jamais le job, c'est un signal |
| `smoke.yml` | Push `main` + PR sur `main` + manuel ; sur une PR, le job `smoke` ne tourne que si le backend change (`src/`, `Directory.Packages.props`, `scripts/smoke.sh`, `docker-compose.yml`) | Bout-en-bout réel (Postgres + Mailpit + API + Hangfire) via `scripts/smoke.sh` | Reproduire en local : `docker compose up -d db mailpit && dotnet run --project src/Api` puis `bash scripts/smoke.sh` |
| `deps-audit.yml` | PR sur `main` + cron hebdo (lundi 05h UTC) + manuel ; sur une PR, `dotnet-audit` et `npm-audit` ne tournent que si un manifeste de deps change (`Directory.Packages.props`, `packages.lock.json`, `package.json`/`package-lock.json`) | Bloque sur CVE High/Critical — `dotnet-audit` (NuGet, direct + transitif) et `npm-audit` (front Angular, dépendances de prod seulement) | Corriger (bump/pin) ou documenter une exception datée — voir § Procédure d'exception CVE ci-dessous |
| `zizmor.yml` | Push `main` touchant `.github/workflows/**` + toute PR ; sur une PR, le job `zizmor` ne tourne que si `.github/workflows/**` change | Lint de sécurité des workflows GitHub Actions (permissions, injections, cache-poisoning…) — mode console (`advanced-security: false`), exit non nul = bloquant | Reproduire en local : `uvx zizmor .github/workflows/` ; corriger le finding (permissions minimales, pin SHA, échappement d'input) |
| `stryker.yml` | PR sur `main` + cron hebdo + manuel ; sur une PR, seul `stryker-domain` tourne, et seulement si le Domain, ses tests, les dépendances ou la configuration de Stryker changent | Mutation testing des tests unitaires : `stryker-domain` **bloquant** (seuil 60 %), `stryker-application` mesuré seulement | Ouvrir le rapport HTML en artifact ; voir § Mutation testing ci-dessous |
| `release.yml` | Tag `v*` + manuel | Build + push image GHCR, SBOM (CycloneDX backend + npm du front), attestation de provenance (SLSA build L2), GitHub Release avec les notes du CHANGELOG et les SBOM (tag seulement) | Voir `DEPLOYMENT.md` § Release — image, SBOM, attestation de provenance, GitHub Release |
| `codeql.yml` / `security.yml` | Push/PR sur `main` | SAST (CodeQL C#/TS) + secret scanning (gitleaks, règles par défaut) | Voir l'onglet Security du repo ; reproduire gitleaks en local : `gitleaks git .` |

> **CodeQL** : gratuit sur un dépôt public. Sur un dépôt privé sans GitHub Advanced Security,
> le job `analyze` est ignoré au lieu d'échouer.

> **Merge queue** : si la merge queue GitHub est activée sur `main`, ajouter le
> déclencheur `merge_group:` à chaque workflow qui porte une vérification exigée (seul
> `ci.yml` l'a aujourd'hui) ; sinon la queue attend indéfiniment un statut qui ne sera
> jamais posté. Le job `changes` ne tourne que sur `pull_request` : dans un run de merge
> queue, les jobs qu'il commande tourneraient toujours.

### Vérifications exigées sur `main`

La règle de protection de `main` exige ces vérifications, désignées par le nom de leur job :

| Workflow | Vérifications exigées | Sur une PR |
|---|---|---|
| `ci.yml` | `doc-lint`, `dotnet`, `web-angular`, `e2e-angular` | Toujours |
| `security.yml` | `gitleaks` | Toujours |
| `codeql.yml` | `Analyze (csharp)`, `Analyze (javascript-typescript)` | Toujours (ignorées sur un dépôt privé, cf. ci-dessus) |
| `deps-audit.yml` | `dotnet-audit`, `npm-audit` | Si un manifeste de dépendances change |
| `smoke.yml` | `smoke` | Si le backend change |
| `stryker.yml` | `stryker-domain` | Si le Domain, ses tests, les dépendances ou la configuration de Stryker changent |
| `zizmor.yml` | `zizmor` | Si un workflow change |

Les quatre derniers workflows se déclenchent pourtant sur les PR sans filtre `paths` : un
workflow filtré ne poste aucun statut, et une PR qui ne touche pas ses chemins attendrait
indéfiniment la vérification exigée (« Expected »), comme l'explique la
[documentation de GitHub](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks#handling-skipped-but-required-checks). Leur job `changes` lance
`scripts/pr-changes.sh`, qui compare le commit de fusion de la PR à sa base (son premier
parent) avec les motifs du workflow, ceux de l'ancien filtre `paths`.

- **Aucun fichier ne correspond** : le job `changes` répond `false`, et le job vérifié est
  ignoré par son `if`. GitHub compte un job ignoré ainsi comme réussi, et la PR peut être
  fusionnée.
- **Un fichier correspond** : le job tourne, et son échec bloque la PR.
- **La détection échoue** (le job `changes` est en échec, ou ne répond ni `true` ni `false`) :
  le job vérifié tourne quand même. Une détection en panne ne laisse donc rien passer sans
  contrôle.
- **Hors PR** : le job `changes` est ignoré, et les jobs vérifiés tournent à chaque
  déclenchement du workflow (cron, push sur `main` ou lancement manuel, selon le workflow ; le
  push de `zizmor.yml` garde son filtre sur `.github/workflows/**`).

Un run annulé avant que le job vérifié démarre ne le lance pas (`!cancelled()`). La
[documentation de GitHub](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-cancellation)
ne dit pas quelle conclusion reçoit alors un job qui n'a pas démarré : s'il est marqué ignoré,
il compte comme réussi. Relancer un run annulé avant de fusionner la PR.

Pour ajouter un verrou de ce type, reprendre le job `changes` d'un de ces workflows et la
condition `if` de ses jobs. Renommer un job exigé change le nom de la vérification : mettre à
jour la règle de protection en même temps, sinon toutes les PR attendent l'ancien nom.

### Dependabot — mises à jour de dépendances

`.github/dependabot.yml` ouvre chaque lundi des PR groupées par écosystème : NuGet (mineures et
correctifs), npm (trois groupes : Angular, vitest et `@vitest/*`, puis les autres mineures et
correctifs ; `@hey-api/openapi-ts`, pré-1.0, garde sa propre PR), GitHub Actions et images Docker.
Un délai de 7 jours (`cooldown`) écarte les versions publiées trop récemment. Les montées majeures
arrivent en PR séparées, sauf celles de vitest, qui reste groupé avec ses plugins ; celles
d'Angular, et les montées majeures et mineures de TypeScript, sont écartées. Les mises à jour de
sécurité, elles, partent sans délai (voir `security/README.md`).

Le groupe `npm-minor-patch`, qui reçoit le reste, porte `patterns: ["*"]`. Quand un paquet relève
de plusieurs groupes, un groupe à motifs le cède au groupe plus « spécifique » ; un groupe sans
motif ne cède rien, et Dependabot le juge plus spécifique qu'un motif à joker comme `@angular/*` ou
`@vitest/*`. Jusqu'à cette correction, les paquets Angular et `@vitest/coverage-v8` restaient donc
dans `npm-minor-patch`, et vitest et `@vitest/coverage-v8` montaient de version majeure dans des PR
séparées : #19, #26 et #30 pour vitest, #21 pour `@vitest/coverage-v8`.

### Procédure d'exception CVE

Documentée intégralement dans `security/README.md` (§ Procédure d'exception) : ouvrir un ticket
de suivi, documenter dans `security/deps-review-*.md` (copier `deps-review-TEMPLATE.md`), ne
jamais désactiver `deps-audit.yml` pour contourner le blocage. `security/README.md` est écrit
pour être lisible par un auditeur externe sans contexte préalable.

### Couverture de code

`ci.yml` collecte la couverture backend via l'extension Microsoft Testing Platform
`Microsoft.Testing.Extensions.CodeCoverage` (`dotnet test --coverage --coverage-output-format cobertura`)
et publie les rapports en artifact (`coverage-backend`, un fichier `<guid>.cobertura.xml` par
projet de test). ReportGenerator, outil local déclaré dans `.config/dotnet-tools.json`, les réunit
ligne par ligne (une ligne est couverte dès qu'un projet de test l'exécute), hors migrations EF et
code généré, et affiche le résultat par couche dans le résumé du run. Pour obtenir le même rapport
en local, en HTML dans `coverage/report/index.html` :

```bash
dotnet test --coverage --coverage-output-format cobertura --results-directory ./coverage
dotnet tool restore
dotnet reportgenerator "-reports:coverage/**/*.cobertura.xml" -targetdir:coverage/report "-filefilters:-*Migrations*;-*.g.cs;-*.generated.cs"
```

La couverture est suivie, pas bloquante : la qualité des tests est mesurée par le mutation testing
(ci-dessous).

---

## Mutation testing (Stryker.NET)

Stryker introduit des mutations dans le code (une condition inversée, une constante changée…)
et vérifie que les tests unitaires les détectent. Un mutant qui survit signale un comportement
qu'aucun test ne vérifie vraiment.

| Périmètre | Config | Score | Seuil `break` |
|---|---|---|---|
| Domain | `stryker-config-domain.json` | ~88 % | 60 % (**bloquant**) |
| Application | `stryker-config-application.json` | ~43 % | 0 (mesuré seulement) |

**Pourquoi le score de l'Application est plus bas.** Stryker ne lance que les tests unitaires.
Les validateurs et les handlers sans accès direct à la base ont des tests unitaires
(`src/tests/UnitTests/Application/`). Ceux qui lisent avec Dapper (connexion, rafraîchissement,
invitations, requêtes) sont couverts par les tests d'intégration sur PostgreSQL
(`src/tests/IntegrationTests/`, Testcontainers), que Stryker ne lance pas : leurs mutants
survivent sans que la logique soit pour autant non testée. Le score est publié pour suivre son
évolution, sans seuil.

`stryker.yml` lance les deux mesures chaque lundi et à la demande, et la mesure du Domain sur
chaque PR qui touche le Domain ou ses tests ; le job `stryker-domain` échoue sous le seuil, et
c'est une vérification exigée sur `main` (cf. § Vérifications exigées sur `main`). Stryker 5
utilise le runner Microsoft Testing Platform (`"test-runner": "mtp"`), celui des projets xunit.v3.

### Lancer une analyse

Stryker est un outil local du dépôt (`.config/dotnet-tools.json`), installé par
`dotnet tool restore`. Il se lance depuis le projet de tests unitaires :

```bash
cd src/tests/UnitTests
dotnet stryker -f ../../../stryker-config-domain.json        # Domain, 1 à 2 min
dotnet stryker -f ../../../stryker-config-application.json   # Application, 1 à 2 min
```

Le rapport HTML est écrit dans `src/tests/UnitTests/StrykerOutput/<date>/reports/mutation-report.html`
(dossier ignoré par git).

### Configuration

Chaque config cible un projet (`Saas.Domain.csproj` ou `Saas.Application.csproj`), exclut les
fichiers générés (`.g.cs`) et désactive l'analyse de couverture (`"coverage-analysis": "off"`).

---

## Outillage développeur

Démarrage local : voir le Quickstart du `README.md` (`.env`, clé JWT en user-secrets,
API sur http://localhost:5123, Angular sur http://localhost:4200).

```bash

# Format & lint
dotnet format                      # .NET
cd web-angular && npm run fix      # Angular
```
