# Security — gestion des dépendances

Ce dossier est la trace documentée des décisions de sécurité liées aux
dépendances (NuGet + npm) : ce qui a été patché, ce qui a été volontairement
laissé en l'état (et pourquoi), et à quelle échéance c'est réexaminé. Il est
écrit pour être lisible **par un auditeur externe** sans contexte préalable.

---

## Vue d'ensemble du processus

| Cadence | Mécanisme | Rôle |
|---|---|---|
| À chaque PR touchant les manifests de dépendances | `.github/workflows/deps-audit.yml` | Bloque le merge si CVE High/Critical (direct ou transitif) |
| Hebdomadaire (lundi 05h UTC) | `deps-audit.yml` (cron) | Attrape les CVE publiées sans changement de code |
| Hebdomadaire (lundi 05h Europe/Zurich) | Dependabot (`.github/dependabot.yml`) | PR automatiques de mise à jour, groupées par écosystème |
| Continu, à chaque build | `NuGetAuditLevel=low` (`Directory.Build.props`) | Filet local : toute sévérité NuGet remonte en warning (→ erreur via `TreatWarningsAsErrors`) dès `dotnet build`, sans attendre le CI |
| Ad hoc (avant merge, avant release) | Revue manuelle datée (ce dossier) | Documente une décision fix/pin/exception |

---

## Outils

### 1. `.github/workflows/deps-audit.yml`

Deux jobs indépendants, tous deux bloquants (`exit 1` si vulnérabilité
High/Critical) :

- **`dotnet-audit`** : `dotnet list Saas.slnx package --vulnerable --include-transitive --format json`,
  parsing du JSON (schéma vérifié localement — voir le rapport de tâche), échoue
  si un package (top-level ou transitif) a une vulnérabilité `High`/`Critical`.
  En cas d'échec sur une PR, un commentaire automatique est posté (job
  `dotnet-audit`, étape « Commentaire PR »).
- **`npm-audit`** : front `web-angular`,
  `npm audit --omit=dev --audit-level=high` (dépendances de dev exclues — un
  CVE dans ESLint/Vitest ne part pas en prod).

Déclencheurs : PR (si les manifests changent), cron hebdomadaire, et
`workflow_dispatch` (relance manuelle).

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
- Les actions GitHub restent épinglées par SHA : Dependabot met à jour le SHA et
  le commentaire de version ensemble.

### 3. `NuGetAuditLevel=low` — `Directory.Build.props`

Défense en profondeur locale : dès `dotnet restore`/`dotnet build`, **toute**
vulnérabilité connue (y compris `Low`/`Moderate`) remonte en warning, et
`TreatWarningsAsErrors=true` en fait une erreur de build. Le tri
High/Critical *bloquant en CI* reste porté par `deps-audit.yml` — ce réglage
est volontairement plus strict en local, pour que le développeur voie le
signal avant même d'ouvrir une PR.

### 4. Revue manuelle datée

Avant un merge sensible, une release, ou à échéance d'une exception
documentée : copier `deps-review-TEMPLATE.md` → `security/deps-review-YYYY-MM-DD.md`,
remplir, committer. Le fichier n'est jamais supprimé — il constitue la piste
d'audit.

---

## Seuils de sévérité

Deux gardes-fous distincts, cumulatifs — c'est leur combinaison qui protège :

- **`deps-audit.yml` (CI, triage)** : ne bloque que High/Critical, côté NuGet comme
  côté npm (`--audit-level=high`). Le restore de ce workflow neutralise
  volontairement `NuGetAuditLevel`/`TreatWarningsAsErrors`
  (`-p:NuGetAudit=false -p:TreatWarningsAsErrors=false`) pour ne pas exploser sur
  une CVE Low/Moderate avant que le parseur JSON ait pu faire le tri de sévérité.
- **Build normal NuGet (`Directory.Build.props`, `NuGetAuditLevel=low` +
  `TreatWarningsAsErrors=true`)** : plus strict — **toute** sévérité (y compris
  Low/Moderate) remonte en warning puis erreur dès `dotnet build`/`dotnet restore`,
  sans attendre le CI. Ce garde-fou local n'existe pas côté npm (pas d'équivalent
  « audit à chaque build » configuré pour le front).

| Sévérité | Action CI (`deps-audit.yml`, triage High/Critical) | Action build normal (NuGet uniquement) | Action humaine attendue |
|---|---|---|---|
| **Critical** | ❌ Bloque le merge | ❌ Bloque le build (`NuGetAuditLevel=low` + `TreatWarningsAsErrors`) | Corriger sous 24h ou exception documentée immédiate |
| **High** | ❌ Bloque le merge | ❌ Bloque le build | Corriger sous 7 jours ou exception documentée |
| **Moderate** | 🟡 Signalé côté npm (non bloquant, `--audit-level=high`) ; non remonté côté NuGet (le triage `deps-audit.yml` filtre sur High/Critical) | ❌ Bloque le build (NuGet) | Revue à la prochaine échéance documentée |
| **Low** | Non remonté (ni NuGet ni npm — le triage CI filtre sur High/Critical) | ❌ Bloque le build (NuGet uniquement) | Revue annuelle |

---

## Procédure d'exception

Quand une CVE ≥ High ne peut pas être corrigée immédiatement (pas de patch
upstream, breaking change non absorbable dans le délai, etc.) :

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

## Convention de nommage

- `security/deps-review-YYYY-MM-DD.md` — une revue par événement déclencheur
  (merge sensible, release, échéance d'exception atteinte).
- Les revues ne sont jamais supprimées ni réécrites — nouvelle date, nouveau
  fichier.

---

## Accès rapide auditeur

- **Politique** : ce fichier.
- **Configuration des contrôles** : `.github/workflows/deps-audit.yml`,
  `.github/dependabot.yml`, `Directory.Build.props` (`NuGetAuditLevel`).
- **Preuve d'exécution** : historique des runs GitHub Actions du workflow
  *Dependencies audit (CVE)*.
- **Décisions historiques et exceptions actives** : fichiers
  `security/deps-review-*.md`.
- **Procédure d'escalade** : section « Procédure d'exception » ci-dessus.
