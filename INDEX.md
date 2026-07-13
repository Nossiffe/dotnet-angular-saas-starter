<!--
  Carte de redirection du dépôt. Une seule table « question → fichier » : pas
  de contenu dupliqué ici, seulement des liens vers la source canonique.
  Si un lien casse, `scripts/check-docs.sh` doit le signaler.
-->

# INDEX

Tu cherches une info sur ce dépôt ? Trouve ta question ci-dessous, ouvre le
fichier indiqué.

| Question | Fichier |
|---|---|
| Architecture / décisions techniques | `ARCHITECTURE.md`, `docs/adr/` |
| Comment implémenter X (use-case, agrégat, query...) | `.claude/patterns/README.md` + skills (`CLAUDE.md` § skills) |
| Règles par zone (backend, fronts, sécurité, tests) | `.claude/rules/*.md` (liste ci-dessous) |
| Messages / codes d'erreur métier | `.claude/rules/error-codes.md` |
| Sécurité / isolation multi-tenant | `.claude/rules/security.md` |
| Workflow dev, hooks, CI | `CONTRIBUTING.md` |
| Déploiement | `DEPLOYMENT.md` |
| Checklist de review | `REVIEW.md` |
| Historique specs / plans (chantiers passés) | `docs/superpowers/` |

## Règles par zone (`.claude/rules/`)

- `dotnet.md` — conventions backend C#/.NET (CQRS, ErrorOr, Vogen).
- `analyzer-cheatsheet.md` — warnings analyzers récurrents (`src/**/*.cs`) et leurs fixes directs.
- `angular.md` — conventions front Angular.
- `react.md` — conventions front React.
- `security.md` — isolation tenant, authz, fail-closed.
- `testing.md` — conventions des tests d'intégration.
- `error-codes.md` — registre des codes d'erreur métier.

## Autres repères

- Skills de scaffolding (`add-use-case`, `add-aggregate`, ...) : table dans `CLAUDE.md` § « Plugins / Skills disponibles ».
- Subagents de review architecturale : table dans `CLAUDE.md` § « Subagents ».
- Feature hors-piste (non couverte par un skill) : plugin `feature-dev`.
