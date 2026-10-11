# dotnet-angular-saas-starter

[![CI](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/ci.yml/badge.svg)](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/codeql.yml/badge.svg)](https://github.com/Nossiffe/dotnet-angular-saas-starter/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-3987E5.svg)](LICENSE)

A SaaS starter with Clean Architecture, DDD and CQRS: **.NET 10**, **Angular 22** and **PostgreSQL 17**, with the quality and security gates I rely on for business-critical platforms.

## What it demonstrates

- **Architecture**: Clean Architecture layers with a framework-free Domain, rich DDD aggregates with strongly-typed IDs (Vogen), CQRS with Mediator and FluentValidation, EF Core for writes and Dapper for reads, ErrorOr results instead of business exceptions.
- **Multi-tenancy**: EF Core global query filters driven by a `tenant` claim. A resource owned by another tenant returns 404, never 403, so its existence is not disclosed. Every route of an organization also reads the caller's membership and role from the database: a token issued before a role change gets a 401, and the front end refreshes the session to get the current role.
- **Robustness**: transactional outbox for domain events with at-least-once delivery: one transaction per message, several API instances sharing the work through row locks, delayed retries that skip the handlers that already succeeded. Invitation e-mails go through it: the link's token is created when the e-mail is sent and only its hash is stored, and a duplicate e-mail replaces the previous link. Hangfire background jobs (the password reset e-mail, retried three times), HybridCache, database retry on transient failures.
- **Observability**: Serilog and OpenTelemetry (OTLP), with the Aspire dashboard locally.
- **API and front end**: minimal APIs with versioning and OpenAPI, TypeScript API types generated from the OpenAPI contract with Hey API, Angular 22 signal-first and zoneless with Angular Material.
- **Security by default**: ASP.NET Core Identity with PBKDF2 at 210,000 iterations (OWASP), account lockout, rate limiting, JWT access tokens with the refresh token in an HttpOnly cookie, HSTS.

## Built with an AI coding agent under strict guardrails

Every change has to pass these gates, whoever or whatever wrote it:

| Gate | Tooling | When |
|---|---|---|
| Build | .NET analyzers, warnings treated as errors (NuGet advisories excepted, see Dependencies) | Every build |
| Architecture rules | Architecture tests: dependency rules on the assemblies' actual references, conventions, ErrorOr usage, security conventions, and a source scan: every raw SQL string literal (`"""…"""`) naming a tenant-owned table must contain the `"TenantId" = @TenantId` filter | Every CI run |
| API contract | OpenAPI document rebuilt and compared with the committed one the front end types come from | Every CI run |
| Behaviour | Unit tests, integration tests against a real PostgreSQL (Testcontainers), end-to-end smoke test (API + PostgreSQL + Mailpit + Hangfire) | Every CI run; smoke test on backend changes |
| Front end | Lint, format, type check, unit tests, Playwright tests of the login, invitation acceptance, Products and Organization pages with axe accessibility checks (API mocked) | Every CI run |
| End-to-end journey | Playwright against the real backend, nothing mocked: the API's Docker image, PostgreSQL and Mailpit. Sign-up with the confirmation e-mail read in Mailpit, sign-in, a product added, edited and deleted, an Admin invited and their e-mail, acceptance, the Admin on the Organization screen, then removed and losing access; axe on every page it goes through. The rate limiting stays as in production | Every CI run |
| Test quality | Stryker mutation testing of the unit tests: blocking for the Domain, measured for the Application | Pull requests touching the Domain, weekly |
| Static analysis | CodeQL for C# and TypeScript | Push, pull request, weekly |
| Secrets | gitleaks with its default rules | Pre-commit hook (when gitleaks is installed) and CI |
| Dependencies | Audit blocking High/Critical CVEs (NuGet including transitive, npm); NuGet advisories of every severity shown as build warnings, without failing the build; grouped Dependabot updates with a 7-day cooldown | Audit: pull requests touching manifests, weekly. Warnings: every build |
| CI security | zizmor workflow linting, actions pinned by SHA | Workflow changes |
| Supply chain | Docker image of the API on GHCR with a build provenance attestation; GitHub Release with the CHANGELOG notes and two CycloneDX SBOMs (API, front end) | Every `v*` tag |

On `main`, the checks of the gates that run on pull requests are required: `doc-lint`, `dotnet`, `web-angular`, `e2e-angular`, `gitleaks`, `Analyze (csharp)`, `Analyze (javascript-typescript)`, `dotnet-audit`, `npm-audit`, `stryker-domain`, `smoke` and `zizmor`. The last five only concern part of the files. On a pull request that does not change those files, a `changes` job skips them, and GitHub [counts a job skipped this way as passed](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks#handling-skipped-but-required-checks). If that detection fails, they run.

Mutation testing runs on the unit tests only. The Domain scores 88 % against a blocking threshold of 60 %. The Application scores about 43 % and is not gated: the handlers that read with Dapper (login, refresh, invitations, queries) are covered by the integration tests against PostgreSQL, which Stryker does not run.

Design decisions are recorded as ADRs in [`docs/adr`](docs/adr). How the project got here is in [Project history](#project-history).

## Quickstart

Requirements: .NET SDK 10, Node.js 24, Docker.

```bash
# 1. Environment variables for Docker Compose
cp .env.example .env

# 2. PostgreSQL, Adminer, Mailpit, Aspire dashboard
docker compose up -d

# 3. JWT signing key for local development, kept out of the repository (once)
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/Api

# 4. API → http://localhost:5123 (OpenAPI: http://localhost:5123/openapi/v1.json)
#    Migrations are applied at startup.
dotnet run --project src/Api

# 5. Angular front end, in another terminal → http://localhost:4200
cd web-angular && npm ci && npm start
```

Confirmation, invitation and password reset e-mails land in Mailpit (http://localhost:8025). To run the API in Docker instead of `dotnet run`, use `docker compose --profile full up -d`.

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

`README.md`, `web-angular/README.md` and `SECURITY.md` are in English, as are identifiers, test names and the commit messages since October 2026. Code comments, API error messages, the Angular interface, the other documents listed below and the changelog are in French.

| File | Content |
|---|---|
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Layers, DDD, CQRS, request lifecycle, robustness, caching |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Workflow, conventions, tests, CI workflows |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | Docker image, production secrets, migration bundles, health probes, release |
| [`docs/adr`](docs/adr) | Architecture decision records |
| [`docs/error-codes.md`](docs/error-codes.md) | Business error registry |
| [`SECURITY.md`](SECURITY.md) | How to report a vulnerability |
| [`security/README.md`](security/README.md) | Dependency security policy |

## Local ports

| Service | URL |
|---|---|
| .NET API | http://localhost:5123 |
| Angular | http://localhost:4200 |
| Adminer | http://localhost:8080 |
| Mailpit | http://localhost:8025 |
| Aspire dashboard | http://localhost:18888 |

## Project history

- **June–July 2026: first version.** Built with an AI coding agent, under my direction. Commit subjects from that period still use the agent's working vocabulary (numbered plans, agent configuration): the history is kept as it was written. The `Co-Authored-By` trailer was adopted later, so those commits do not carry it.
- **October 2026: hardening.** A review of the whole repository by a separate AI agent, which had not written the code, then three pull requests acting on its findings, each reviewed and merged by me:
  - [#7](https://github.com/Nossiffe/dotnet-angular-saas-starter/pull/7): CI, Quickstart, mutation testing and documentation brought in line with what this README claims.
  - [#8](https://github.com/Nossiffe/dotnet-angular-saas-starter/pull/8): authentication and multi-tenant isolation: login timing oracle, malformed tokens, sessions after a removal from an organization, a single organization scope check, a working Admin role, one log per request.
  - [#9](https://github.com/Nossiffe/dotnet-angular-saas-starter/pull/9): architecture guarantees: a framework-free Domain, ErrorOr for every business refusal, an outbox with one transaction per message and delayed retries, an OpenAPI contract checked by the CI, integration tests with a single possible outcome.

## License

[MIT](LICENSE)
