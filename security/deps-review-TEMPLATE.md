# Revue dépendances — YYYY-MM-DD

**Date de rédaction** : YYYY-MM-DD
**Réviseur** : <nom>
**Revue précédente** : [deps-review-YYYY-MM-DD.md](./deps-review-YYYY-MM-DD.md) (ou « aucune » si première revue)
**Prochaine revue prévue** : YYYY-MM-DD

---

## 1. Résumé

- CVE corrigées depuis la dernière revue : N
- Exceptions actives (voir tableau §3) : N
- Dépendances ≥ 2 versions majeures de retard : N
- État `deps-audit.yml` depuis la dernière revue : X vert / Y rouge

## 2. CVE corrigées depuis la dernière revue

| Package | Version avant → après | Sévérité | CVE / advisory | Date correction | Écosystème |
|---|---|---|---|---|---|
| _ex._ Newtonsoft.Json | 9.0.1 → 13.0.3 | High | GHSA-5crp-9r3c-p9vr | YYYY-MM-DD | nuget |

_Si vide, écrire « Aucune »._

## 3. Tableau des exceptions actives

Table vivante : reporter d'une revue à l'autre toute ligne non résolue.
Une ligne sort du tableau uniquement quand la CVE est corrigée (→ passer en §2)
ou que le risque est formellement accepté sans échéance (rare, à justifier
explicitement).

| Package | CVE / advisory | Sévérité | Décision | Justification | Mitigation en place | Échéance de réexamen |
|---|---|---|---|---|---|---|
| _ex._ some-transitive-pkg | GHSA-yyyy | Moderate | Pin version intermédiaire | Pas de patch upstream, breaking change sur la version corrigée | Chemin de code non atteignable sans auth | YYYY-MM-DD |

_Si vide, écrire « Aucune »._

## 4. Dépendances ≥ 2 versions majeures de retard

| Package | Version actuelle | Dernière stable | Décision | Justification |
|---|---|---|---|---|
| _ex._ NodaTime | 3.0.x | 5.0.x | Reportée | API breaking, upgrade planifiée |

_Si vide, écrire « Aucune »._

## 5. Actions ouvertes

- [ ] _ex._ Vérifier que le groupage Dependabot `nuget-minor-patch` ne mélange pas des majors
- [ ] _ex._ Réexaminer l'exception `some-transitive-pkg` avant YYYY-MM-DD

## 6. Santé des contrôles

- **`dotnet-audit`** (deps-audit.yml) — dernier run vert : YYYY-MM-DD
- **`npm-audit`** (deps-audit.yml, web-angular) — dernier run vert : YYYY-MM-DD
- **Runs rouges depuis la dernière revue** : N (lien vers les runs si pertinent)
- **PR Dependabot fusionnées depuis la dernière revue** : N

## 7. Validation

- Réviseur : <signature>
- Décision globale : merge/release autorisé sans réserve / autorisé avec exceptions documentées ci-dessus / bloqué (préciser)
