# dotnet-angular-saas-starter

[![CI](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/ci.yml/badge.svg)](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/codeql.yml/badge.svg)](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-3987E5.svg)](LICENSE)

A SaaS starter with Clean Architecture, DDD and CQRS: **.NET 10**, **Angular 22** and **PostgreSQL 17**, with the quality and security gates I rely on for business-critical platforms.

## What it demonstrates

- **Architecture**: Clean Architecture layers, rich DDD aggregates with strongly-typed IDs (Vogen), CQRS with Mediator and FluentValidation, EF Core for writes and Dapper for reads, ErrorOr results instead of business exceptions.
- **Multi-tenancy**: EF Core global query filters driven by a `tenant` claim. A resource owned by another tenant returns 404, never 403, so its existence is not disclosed.
- **Robustness**: transactional outbox for domain events, Hangfire background jobs, HybridCache, database retry and HTTP resilience policies.
- **Observability**: Serilog and OpenTelemetry (OTLP), with the Aspire dashboard locally.
- **API and front end**: minimal APIs with versioning and OpenAPI, a typed Angular client generated with Hey API, Angular 22 signal-first and zoneless with Angular Material.
- **Security by default**: ASP.NET Core Identity with PBKDF2 at 210,000 iterations (OWASP), account lockout, rate limiting, JWT access tokens with the refresh token in an HttpOnly cookie, HSTS.

## Built with an AI coding agent under strict guardrails

Every change has to pass these gates, whoever or whatever wrote it:

| Gate | Tooling | When |
|---|---|---|
| Build | .NET analyzers, warnings treated as errors | Every build |
| Architecture rules | Architecture tests: dependency rules, conventions, ErrorOr usage, security conventions | Every CI run |
| Behaviour | Unit tests, integration tests against a real PostgreSQL (Testcontainers), end-to-end smoke test (API + PostgreSQL + Mailpit + Hangfire) | Every CI run |
| Front end | Lint, format, type check, unit tests, Playwright end-to-end tests with axe accessibility checks | Every CI run |
| Test quality | Stryker mutation testing (Application and Domain) | Weekly |
| Static analysis | CodeQL for C# and TypeScript | Push, pull request, weekly |
| Secrets | gitleaks | Pre-commit hook and CI |
| Dependencies | Audit blocking High/Critical CVEs (NuGet including transitive, npm), `NuGetAuditLevel=low` locally, grouped Dependabot updates with a 7-day cooldown | Pull requests touching manifests, weekly |
| CI security | zizmor workflow linting, actions pinned by SHA | Workflow changes |
| Supply chain | CycloneDX SBOM and build provenance attestation | Every release |

Design decisions are recorded as ADRs in [`docs/adr`](docs/adr). Commits written together with the agent carry a `Co-Authored-By` trailer.

## Quickstart

```bash
# 1. Shared Docker network (once per machine)
docker network create devnet 2>/dev/null || true

# 2. Environment variables
cp .env.example .env

# 3. PostgreSQL, Adminer, Mailpit, Aspire dashboard
docker compose up -d

# 4. API → http://localhost:5123 (OpenAPI: http://localhost:5123/openapi/v1.json)
dotnet run --project src/Api

# 5. Angular front end, in another terminal → http://localhost:4200
cd web-angular && npm install && npm start
```

## Key commands

| Command | Purpose |
|---|---|
| `./scripts/check.sh` | Full gate: build, all tests, front-end checks (Docker required) |
| `./scripts/check-fast.sh` | Quick gate: build and format verification |
| `dotnet test` | Unit, integration and architecture tests (Docker required) |
| `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api` | New EF Core migration |
| `cd web-angular && npm run check` | Front-end CI gate |
| `cd web-angular && npm run fix` | Front-end lint and format |

## Documentation

The architecture notes, ADRs and contribution guide are written in French.

| File | Content |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Layers, DDD, CQRS, request lifecycle, robustness, caching |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Workflow, conventions, tests, CI workflows |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | Docker image, production secrets, migration bundles, health probes, release |
| [`docs/adr`](docs/adr) | Architecture decision records |
| [`docs/error-codes.md`](docs/error-codes.md) | Business error registry |
| [`security/README.md`](security/README.md) | Dependency security policy |

## Local ports

| Service | URL |
|---|---|
| .NET API | http://localhost:5123 |
| Angular | http://localhost:4200 |
| Adminer | http://localhost:8080 |
| Mailpit | http://localhost:8025 |
| Aspire dashboard | http://localhost:18888 |

## License

[MIT](LICENSE)
