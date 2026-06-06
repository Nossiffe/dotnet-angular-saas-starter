# Saas

Template SaaS — .NET 10 (Clean Architecture) + Angular 22 / React + PostgreSQL.

Voir `docs/superpowers/specs/2026-06-06-saas-template-design.md` pour le design complet.

## Quickstart (provisoire)

```bash
docker network create devnet 2>/dev/null || true   # réseau partagé (une seule fois)
docker compose up -d        # Postgres + Adminer + Mailpit
dotnet build                # build de la solution
dotnet test                 # tests
```
