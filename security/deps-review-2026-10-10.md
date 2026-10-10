# Revue dépendances — 2026-10-10

**Date de rédaction** : 2026-10-10
**Réviseur** : Pierre-Eric Tessa
**Revue précédente** : aucune (première revue)
**Prochaine revue prévue** : 2026-11-09

Période couverte : depuis la mise en ligne du dépôt sur GitHub (commit
b8e8ef5, 2026-10-09) jusqu'au 2026-10-10, sur `main` après la fusion de la
PR #20. Les corrections antérieures à la mise en ligne n'en font pas partie.

---

## 1. Résumé

- CVE corrigées depuis la dernière revue : 37 avis, tous sur des dépendances de développement du front (§2)
- Exceptions actives (voir tableau §3) : 0
- Avis encore signalés par `npm audit` : 1, braces (GHSA-vfj7-8cjw-p6xm, High), dépendance de développement sans version corrigée, laissée en l'état (§3)
- Dépendances ≥ 2 versions majeures de retard : 0
- État `deps-audit.yml` depuis la dernière revue : 27 vert / 0 rouge (runs #1 à #27, tous les runs du workflow sur GitHub)

## 2. CVE corrigées depuis la dernière revue

| Package | Version avant → après | Sévérité | CVE / advisory | Date correction | Écosystème |
|---|---|---|---|---|---|
| colord | 2.9.3 → 2.10.0 | Moderate | GHSA-2wm5-q62r-hmrv | 2026-10-09 (PR #1) | npm, dev |
| postcss-selector-parser | 7.1.4 → 7.1.6 | Moderate | GHSA-rj75-hqrm-r3gf | 2026-10-09 (PR #1) | npm, dev |
| undici | 7.28.0 → 8.11.2 (avec jsdom 30) | High (3), Moderate (9), Low (3) | GHSA-4cwx-7wf7-3272, GHSA-rfgv-xxqx-mfg5, GHSA-w293-vg96-wgc3 (High) ; GHSA-8xcm-r25x-g524, GHSA-m8rv-5g2x-5cg5, GHSA-jr45-8vmc-qm54, GHSA-v3r7-h72x-cjcm, GHSA-2jfj-6hjv-fm6j, GHSA-rx4f-c7p8-82vq, GHSA-pmjh-fq2x-6v4x, GHSA-3xpg-4rpp-hhhm, GHSA-3wwx-pv8p-q78v (Moderate) ; GHSA-r53p-7pc4-xj5r, GHSA-8436-99hf-9mmv, GHSA-2gqq-gqf2-x968 (Low) | 2026-10-09 (PR #4) | npm, dev |
| brace-expansion | 5.0.7 → 5.0.12 | High (4), Moderate (1) | GHSA-mh99-v99m-4gvg, GHSA-rgw5-rvv9-x895, GHSA-6j4f-fj2g-mc7p, GHSA-qhr7-859c-m2p7 (High) ; GHSA-q2hr-2g5m-vwhr (Moderate) | 2026-10-10 (PR #14) | npm, dev |
| fast-uri | 3.1.3 → 3.1.8 | High (7), Moderate (1) | GHSA-v2hh-gcrm-f6hx, GHSA-7p8r-x3mc-p8w7, GHSA-5jgf-p345-68v8, GHSA-f65p-4m7j-42xc, GHSA-fph4-wmhf-6fwf, GHSA-jqff-g426-hqxp, GHSA-qw65-cvwx-89v3 (High) ; GHSA-hrr3-gc8f-f4qj (Moderate) | 2026-10-10 (PR #15) | npm, dev |
| baseline-browser-mapping | 2.10.43 → 2.11.28 | Moderate | GHSA-w5vr-8v7q-w6rv | 2026-10-10 (PR #16) | npm, dev |
| browserslist | 4.28.6 → 4.29.3 | High (2) | GHSA-c83g-rgw3-j3cx, GHSA-73wf-gq98-2v4g | 2026-10-10 (PR #17) | npm, dev |
| js-yaml | 4.2.0 → 4.3.2 (`overrides` npm) | High (3) | GHSA-52cp-r559-cp3m, GHSA-5p4m-2wfm-xmqj, GHSA-2883-xcg3-v3hh | 2026-10-10 (PR #18) | npm, dev |
| vitest, @vitest/mocker | 4.1.10 → 4.1.11 | Moderate | GHSA-82fw-gwwq-j7x9 | 2026-10-10 (PR #18) | npm, dev |

La PR #15 est intitulée `build(deps)`, mais fast-uri, tiré par ajv via
`@angular-devkit/core`, est marqué en dépendance de développement dans
`web-angular/package-lock.json`, comme tous les paquets de ce tableau.

Côté NuGet, `dotnet list package --vulnerable --include-transitive` ne signale
aucune vulnérabilité, toutes sévérités confondues.

## 3. Tableau des exceptions actives

Aucune.

`npm audit`, lancé sans `--omit=dev`, signale encore un avis sur une
dépendance de développement. Nous ne le traitons pas comme une exception : la
règle « Corriger sous 7 jours ou exception documentée » du tableau des seuils
de `security/README.md` figure sur la même ligne que le blocage du merge par
`deps-audit.yml`, dont le job npm exclut les dépendances de développement
(« un CVE dans ESLint/Vitest ne part pas en prod »). Nous la lisons donc comme
portant sur les avis que ce workflow bloque. L'avis est suivi ci-dessous, à
reporter dans chaque revue tant que `npm audit` le cite.

### Avis sur une dépendance de développement, laissé en l'état

| Package | CVE / advisory | Sévérité | Décision | Réexamen |
|---|---|---|---|---|
| braces 3.0.3 (transitive, dev, via stylelint) | GHSA-vfj7-8cjw-p6xm (CVE-2026-93687) | High (CVSS 3.1 : 7,5 ; CVSS 4.0 : 8,7 ; disponibilité seule) | Laissé en l'état : aucune version corrigée, avis non exploitable ici | 2026-11-09 au plus tard |

**L'avis.** braces 3.0.3 et antérieures limitent la longueur d'un motif
(10 000 caractères) mais pas sa profondeur d'imbrication. Un motif de quelques
milliers d'accolades imbriquées épuise la pile des fonctions récursives qui
parcourent l'arbre du motif ; l'exception `RangeError` non rattrapée arrête le
processus Node. L'impact est un déni de service, sans atteinte à la
confidentialité ni à l'intégrité (`C:N/I:N/A:H`). Avis publié le 2026-09-18,
sans version corrigée.

**Pourquoi `npm audit` le signale.** Une seule copie de braces, en dépendance
de développement de `web-angular`
(`npm ls braces micromatch fast-glob globby`) :

```
stylelint@17.16.0
├── fast-glob@3.3.3 ── micromatch@4.0.8
├── globby@16.2.4 ── fast-glob@3.3.3, micromatch@4.0.8
└── micromatch@4.0.8 ── braces@3.0.3
```

stylelint, l'outil de vérification des feuilles SCSS, est lancé par
`npm run lint:styles`, `npm run check` (en local et dans le job `web-angular`
de la CI) et `npm run fix`. L'application ne l'importe pas : rien de cette
chaîne n'entre dans l'application livrée. Le job `npm-audit` de
`deps-audit.yml` (`npm audit --omit=dev`) ne la voit pas ; `npm audit` lancé
sans `--omit=dev` la signale, avec les paquets qui en dépendent : micromatch,
fast-glob, globby, stylelint, son extension `stylelint-scss` et ses
configurations.

**Pourquoi l'avis n'est pas exploitable ici.** braces analyse des motifs de
fichiers (globs), jamais le contenu des fichiers ni leurs noms : ceux-ci sont
seulement comparés aux motifs une fois compilés. Les motifs que stylelint
passe à micromatch, fast-glob et globby sont tous écrits dans le dépôt ou dans
stylelint lui-même :

- `"src/**/*.scss"`, dans les scripts de `web-angular/package.json` ;
- `**/node_modules/**`, ignoré en dur par stylelint ;
- aucun `ignoreFiles`, `overrides` ni `referenceFiles` dans
  `web-angular/.stylelintrc.json`, ni dans les configurations qu'il étend
  (`stylelint-config-standard-scss`, `stylelint-config-standard`,
  `stylelint-config-recommended-scss` et `stylelint-config-recommended`), et
  pas de `.stylelintignore`.

Aucun ne contient d'accolade. Déclencher le défaut suppose de modifier ces
fichiers. Qui le fait, en local ou dans une PR dont la CI exécute la branche,
choisit déjà les commandes lancées, puisque les scripts npm s'exécutent tels
qu'ils sont écrits : l'avis ne lui donne rien de plus. Au pire, une commande
de lint s'arrête, en local ou en CI.

**Côté amont.** Aucune version corrigée n'est publiée. Un mainteneur de
braces conteste la portée de l'avis dans l'issue
[micromatch/braces#70](https://github.com/micromatch/braces/issues/70) : selon
lui, la longueur maximale des motifs, déjà réglable, suffit. Une PR de
correction proposée par un contributeur
([micromatch/braces#72](https://github.com/micromatch/braces/pull/72)) a été
fermée sans fusion. Un mainteneur de stylelint annonce que la version 18.0.0
passera à tinyglobby et picomatch
([stylelint/stylelint#9559](https://github.com/stylelint/stylelint/issues/9559)),
ce qui devrait faire sortir braces de l'arbre.

**Réexamen** au plus tard le 2026-11-09. À chaque revue, vérifier la dernière
version publiée de stylelint (`npm view stylelint version`) et l'état de
l'avis. Réexaminer plus tôt si l'un de ces événements survient :

- stylelint 18.0.0 est publié. Dependabot ne proposera la montée qu'au moins
  7 jours après la publication (`cooldown`), et seulement quand une version de
  `stylelint-config-standard-scss` l'acceptera : la version 17.0.0 exige
  `stylelint ^17.0.0` ;
- une version de braces qui corrige l'avis est publiée ;
- l'avis est retiré ou modifié dans la GitHub Advisory Database ;
- un motif de fichier qui ne vient ni du dépôt ni de stylelint atteint
  stylelint, ou braces apparaît dans une dépendance de production : l'avis
  relèverait alors des seuils du tableau de `security/README.md`.

**Condition de clôture.** Dans `web-angular`, `npm ls braces` ne montre plus
aucune copie de braces, ou seulement une version que l'avis ne vise pas, et
`npm audit` ne cite plus GHSA-vfj7-8cjw-p6xm. Le chemin attendu est
stylelint 18, avec une version de `stylelint-config-standard-scss` qui
l'accepte. L'avis sort alors de cette section à la revue suivante : en §2 si
braces est corrigé ou quitte l'arbre, avec la raison si l'avis est retiré.

## 4. Dépendances ≥ 2 versions majeures de retard

Aucune. Trois montées d'une version majeure sont en attente : vitest avec
`@vitest/coverage-v8` (4 → 5), TypeScript (6 → 7, la version suit celle
qu'exige Angular 22) et xunit.v3 (3 → 4).

## 5. Actions ouvertes

- [ ] Réexaminer l'avis braces avant le 2026-11-09, ou dès l'un des
      déclencheurs du §3
- [ ] Vérifier le premier run planifié de `deps-audit.yml` (cron du lundi
      2026-10-12, 05 h UTC) : les 27 runs à ce jour ont tous été déclenchés
      par des PR
- [ ] Mises à jour NuGet du groupe `nuget-minor-patch` (PR #22, en échec) :
      les reprendre dans une PR qui corrige ce qu'elles cassent
- [ ] Migrer xunit.v3 vers la version 4 (PR #23, en échec : aucun test
      découvert)
- [ ] Monter vitest et `@vitest/coverage-v8` en version 5 ensemble (PR #19
      et #21, ouvertes séparément puis fermées)
- [ ] Réexaminer l'override js-yaml à chaque montée de `@hey-api/openapi-ts`
      (`security/README.md`, « Versions forcées »)

## 6. Santé des contrôles

- **`dotnet-audit`** (deps-audit.yml) — dernier run vert : 2026-10-10 (run #27, PR #23)
- **`npm-audit`** (deps-audit.yml, web-angular) — dernier run vert : 2026-10-10 (run #27, PR #23)
- **Runs rouges depuis la dernière revue** : 0 sur 27
- **PR Dependabot fusionnées depuis la dernière revue** : 7 (#1, #4, #14, #15, #16, #17, #20)

## 7. Validation

- Réviseur : Pierre-Eric Tessa, par la fusion de la PR qui ajoute cette revue
- Décision globale : merge/release autorisé ; aucune exception active
