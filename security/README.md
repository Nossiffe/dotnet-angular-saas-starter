# Security — gestion des dépendances

Ce dossier est la trace documentée des décisions de sécurité liées aux
dépendances (NuGet + npm) : ce qui a été patché, ce qui a été volontairement
laissé en l'état (et pourquoi), et à quelle échéance c'est réexaminé. Il est
écrit pour être lisible **par un auditeur externe** sans contexte préalable.

---

## Vue d'ensemble du processus

| Cadence | Mécanisme | Rôle |
|---|---|---|
| À chaque PR touchant les manifests de dépendances | `.github/workflows/deps-audit.yml` | Bloque le merge si CVE High/Critical (direct ou transitif ; côté npm, dépendances de production seulement) : `dotnet-audit` et `npm-audit` sont des vérifications exigées sur `main` |
| Hebdomadaire (lundi 05h UTC) | `deps-audit.yml` (cron) | Attrape les CVE publiées sans changement de code |
| Hebdomadaire (lundi 05h Europe/Zurich) | Dependabot (`.github/dependabot.yml`) | PR automatiques de mise à jour, groupées par écosystème |
| Continu, à chaque build | `NuGetAuditLevel=low` (`Directory.Build.props`) | Filet local : toute sévérité NuGet remonte en warning dès `dotnet build`, sans attendre le CI et sans faire échouer le build |
| Ad hoc (avant merge, avant release) | Revue manuelle datée (ce dossier) | Documente une décision fix/pin/exception |

---

## Outils

### 1. `.github/workflows/deps-audit.yml`

Deux jobs indépendants, tous deux bloquants (`exit 1` si vulnérabilité
High/Critical) :

- **`dotnet-audit`** : `dotnet list Saas.slnx package --vulnerable --include-transitive --format json`,
  parsing du JSON, échoue
  si un package (top-level ou transitif) a une vulnérabilité `High`/`Critical`.
  En cas d'échec sur une PR, un commentaire automatique est posté (job
  `dotnet-audit`, étape « Commentaire PR »).
- **`npm-audit`** : front `web-angular`,
  `npm audit --omit=dev --audit-level=high` (dépendances de dev exclues — un
  CVE dans ESLint/Vitest ne part pas en prod).

Déclencheurs : toutes les PR vers `main`, cron hebdomadaire, et `workflow_dispatch`
(relance manuelle).

`dotnet-audit` et `npm-audit` sont des vérifications exigées par la règle de protection de
`main`. Sur une PR, un job `changes` (`scripts/pr-changes.sh`) regarde si un manifeste change :
`Directory.Packages.props`, un `packages.lock.json`, un `package.json` ou un
`package-lock.json`.

- Si oui, les deux audits tournent, et un avis High/Critical bloque la PR.
- Si non, ils sont ignorés par leur condition `if`, et GitHub
  [compte un job ignoré ainsi comme réussi](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks#handling-skipped-but-required-checks) : une PR sans changement de dépendances
  n'attend pas l'audit.
- Si la détection échoue ou ne répond pas, les deux audits tournent quand même : une panne de
  la détection ne laisse passer aucune PR sans audit.

Le cron et `workflow_dispatch` lancent toujours les deux audits, sans détection. Le workflow
n'a pas de filtre `paths` : un workflow filtré ne poste aucun statut, et une PR qui ne
toucherait pas les manifests attendrait indéfiniment les vérifications exigées.

### 2. Dependabot — `.github/dependabot.yml`

Points clés :

- `cooldown: default-days: 7` — délai anti-supply-chain : Dependabot ne propose
  pas une version publiée il y a moins de 7 jours (leçon des attaques de type
  « ver npm » 2025 : un package compromis est généralement détecté et retiré
  avant 7 jours).
- Groupage : mises à jour mineures et correctifs groupés par écosystème
  (`nuget-minor-patch`, `angular`, `npm-minor-patch`, `github-actions`, `docker`)
  pour limiter le bruit de PR ; `@hey-api/openapi-ts` (pré-1.0, minors
  potentiellement breaking) reste **hors groupe**, une PR par mise à jour.
  vitest et ses plugins (`@vitest/*`) ont leur propre groupe, montées
  majeures comprises : `@vitest/coverage-v8` exige la version exacte de vitest.
  Le groupe `npm-minor-patch` porte `patterns: ["*"]`, sans quoi il gardait les
  paquets Angular et `@vitest/coverage-v8` (voir `CONTRIBUTING.md`).
- Mises à jour de sécurité : sans délai de cooldown, et une PR par paquet
  vulnérable tant que le réglage « Grouped security updates » du dépôt reste
  désactivé. Exception : vitest et `@vitest/*` forment le groupe
  `vitest-security` (`applies-to: security-updates`), pour que vitest et
  `@vitest/mocker`, que vitest fixe à sa propre version, soient tentés
  ensemble. Ce groupe ne lève pas le blocage principal : `@vitest/coverage-v8`
  n'est visé par aucun avis mais exige la version exacte de vitest, et la
  documentation de GitHub ne dit pas si une mise à jour de sécurité groupée
  monte aussi un paquet sans avis. Si la mise à jour échoue
  (`security_update_not_possible`), monter à la main vitest et
  `@vitest/coverage-v8` ensemble, à la même version.
- Les actions GitHub restent épinglées par SHA : Dependabot met à jour le SHA et
  le commentaire de version ensemble.

### 3. `NuGetAuditLevel=low` — `Directory.Build.props`

Signal local : dès `dotnet restore`/`dotnet build`, **toute** vulnérabilité
connue (y compris `Low`/`Moderate`) remonte en warning, pour que le développeur
la voie avant même d'ouvrir une PR. Ces warnings (NU1901 à NU1904, plus NU1900
quand la source des avis est injoignable) sont exclus de `TreatWarningsAsErrors`
par `WarningsNotAsErrors` : un avis publié du jour au lendemain ne doit pas
empêcher de compiler un clone neuf ou une branche sans rapport. Le blocage
High/Critical reste porté par `deps-audit.yml`.

### 4. Revue manuelle datée

Avant un merge sensible, une release, ou à échéance d'une exception
documentée : copier `deps-review-TEMPLATE.md` → `security/deps-review-YYYY-MM-DD.md`,
remplir, committer. Le fichier n'est jamais supprimé — il constitue la piste
d'audit.

---

## Seuils de sévérité

Un seul contrôle bloquant, `deps-audit.yml`, complété par un signal local qui ne bloque pas :

- **`deps-audit.yml` (CI, triage)** : ne bloque que High/Critical, côté NuGet comme
  côté npm (`--audit-level=high`, dépendances de production seulement). Le
  restore de ce workflow désactive l'audit NuGet (`-p:NuGetAudit=false`) : c'est
  le parseur JSON qui fait le tri de sévérité.
- **Build normal NuGet (`Directory.Build.props`, `NuGetAuditLevel=low`)** : plus
  large mais non bloquant — **toute** sévérité (y compris Low/Moderate) remonte en
  warning dès `dotnet build`/`dotnet restore`, sans attendre le CI. Ce signal local
  n'existe pas côté npm (pas d'équivalent « audit à chaque build » configuré pour
  le front).

| Sévérité | Action CI (`deps-audit.yml`, triage High/Critical) | Action build normal (NuGet uniquement) | Action humaine attendue |
|---|---|---|---|
| **Critical** | ❌ Bloque le merge | ⚠️ Warning NU1904, build non bloqué | Corriger sous 24h ou exception documentée immédiate |
| **High** | ❌ Bloque le merge | ⚠️ Warning NU1903 | Corriger sous 7 jours ou exception documentée |
| **Moderate** | 🟡 Signalé côté npm (non bloquant, `--audit-level=high`) ; non remonté côté NuGet (le triage `deps-audit.yml` filtre sur High/Critical) | ⚠️ Warning NU1902 | Revue à la prochaine échéance documentée |
| **Low** | Non remonté (ni NuGet ni npm — le triage CI filtre sur High/Critical) | ⚠️ Warning NU1901 | Revue annuelle |

Les délais des lignes Critical et High portent sur les avis que
`deps-audit.yml` bloque : côté npm, ceux des dépendances de production
(`--omit=dev`). Les avis sur des dépendances de développement npm sont suivis
dans les revues datées (`deps-review-*.md`), sans délai de correction.

---

## Procédure d'exception

Quand un avis ≥ High bloqué par `deps-audit.yml` ne peut pas être corrigé
immédiatement (pas de patch upstream, breaking change non absorbable dans le
délai, etc.) :

1. Ouvrir un ticket de suivi (CVE, date cible de résolution).
2. Documenter dans la revue courante (`deps-review-*.md`), section « Tableau
   des exceptions actives » : package, CVE, sévérité, justification,
   mitigation en place, échéance de réexamen.
3. Ne **jamais** désactiver ou affaiblir `deps-audit.yml` pour contourner le
   blocage. Alternatives acceptables :
   - **Upgrade** vers une version non vulnérable (préféré) ;
   - **Pin** temporaire d'une version intermédiaire non vulnérable si
     disponible ;
   - Si aucune des deux n'est possible dans l'immédiat : le merge reste
     bloqué — l'exception documente *pourquoi* et *jusqu'à quand*, elle ne
     lève pas le gate technique.

---

## Versions forcées (`overrides` npm)

Quand une version corrigée existe mais qu'une dépendance intermédiaire exige
encore la version vulnérable, `web-angular/package.json` impose la version
corrigée à cette dépendance seule, par un `overrides` limité à sa version
exacte. Chaque override est listé ici avec la condition de son retrait.

| Paquet | Version imposée | À qui | Avis corrigés | Retirer quand |
|---|---|---|---|---|
| `js-yaml` | `^4.3.2`, au lieu de 4.2.0 exactement | `@hey-api/json-schema-ref-parser` 1.4.4, tiré par `@hey-api/openapi-ts` 0.99.0 | GHSA-52cp-r559-cp3m, GHSA-5p4m-2wfm-xmqj, GHSA-2883-xcg3-v3hh (élevés : consommation CPU quadratique sur un YAML piégé) | le parseur tiré par `@hey-api/openapi-ts` accepte un js-yaml corrigé (4.3.2 ou plus, ou 5.x) ; s'il change de version sans le corriger, reporter l'override sur sa nouvelle version |

js-yaml ne sert qu'aux outils de développement : la génération des types de
l'API (`npm run api:gen`) et le chargement de la configuration de stylelint
par `cosmiconfig`, qui accepte `^4.1.0` et partage la copie 4.3.2 du parseur.
4.3.2 reste dans la même version majeure que 4.2.0 ; les types générés depuis
`openapi.json` sont identiques avec l'une et l'autre.

L'override vise la version 1.4.4 du parseur, pas tout l'arbre : les versions
en préparation de Hey API passent à js-yaml 5, et un override global leur
imposerait js-yaml 4 sans qu'aucun contrôle ne le signale. En contrepartie,
une nouvelle version du parseur qui exigerait encore un js-yaml vulnérable
échapperait à l'override : seul `npm audit` le montrerait à coup sûr, car
`deps-audit.yml` ignore les dépendances de développement et GitHub peut fermer
d'office l'alerte Dependabot d'une dépendance de développement npm.

À chaque montée de `@hey-api/openapi-ts`, que Dependabot propose dans une PR
à part, et dans cette même PR :

1. `npm ls @hey-api/json-schema-ref-parser js-yaml` : si le parseur est
   toujours en 1.4.4, rien à faire.
2. Sinon, `npm view @hey-api/json-schema-ref-parser@<nouvelle version> dependencies.js-yaml` :
   - js-yaml 4.3.2 ou plus, ou 5.x : retirer l'override de `package.json`,
     puis lancer `npm install` ;
   - encore un js-yaml vulnérable : reporter l'override sur la nouvelle
     version du parseur.
3. `npm audit` ne doit plus citer ces avis.

---

## Convention de nommage

- `security/deps-review-YYYY-MM-DD.md` — une revue par événement déclencheur
  (merge sensible, release, échéance d'exception atteinte).
- Les revues ne sont jamais supprimées ni réécrites — nouvelle date, nouveau
  fichier.

---

## Accès rapide auditeur

- **Politique** : ce fichier.
- **Configuration des contrôles** : `.github/workflows/deps-audit.yml`,
  `.github/dependabot.yml`, `Directory.Build.props` (`NuGetAuditLevel`). Les vérifications
  exigées sur `main` sont listées dans `CONTRIBUTING.md` (§ Vérifications exigées sur `main`).
- **Preuve d'exécution** : historique des runs GitHub Actions du workflow
  *Dependencies audit (CVE)*.
- **Décisions historiques et exceptions actives** : fichiers
  `security/deps-review-*.md`.
- **Versions imposées par `overrides` npm** : section « Versions forcées »
  ci-dessus.
- **Procédure d'escalade** : section « Procédure d'exception » ci-dessus.
