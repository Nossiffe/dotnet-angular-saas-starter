## Résumé

<!-- Décrivez brièvement le changement et son contexte. -->

## Type de changement

- [ ] `feat` — nouvelle fonctionnalité
- [ ] `fix` — correction de bug
- [ ] `refactor` — refactoring sans changement de comportement
- [ ] `test` — ajout/modification de tests
- [ ] `docs` — documentation uniquement
- [ ] `chore` — outillage, config, dépendances
- [ ] `ci` — pipelines CI/CD
- [ ] `build` — système de build

## Checklist

- [ ] Les tests unitaires et d'intégration passent (`dotnet test`)
- [ ] `./scripts/check.sh` est vert (build + tests + front)
- [ ] Un ADR a été créé si la PR implique une décision d'architecture significative
- [ ] Aucun secret/credential n'est inclus dans les fichiers committés
- [ ] La migration EF a été relue avant commit (si applicable)
- [ ] Les clients API ont été régénérés (`npm run api:gen` / `npm run gen:api`) si l'OpenAPI a changé

## Notes de revue

<!-- Informations utiles pour le reviewer : choix techniques, compromis, zones à regarder en priorité. -->
