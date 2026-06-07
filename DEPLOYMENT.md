# DEPLOYMENT.md — Guide de déploiement

Ce guide couvre la construction de l'image Docker, les variables d'environnement requises, la stratégie de migrations, les health probes et les notes de scaling.

---

## Image Docker

Le `Dockerfile` (multi-stage, utilisateur non-root) produit une image légère basée sur `mcr.microsoft.com/dotnet/aspnet:10.0`.

```bash
# Build
docker build -t <registry>/<app>:<tag> .

# Run (exemple minimal — voir "Variables d'env" ci-dessous pour les secrets requis)
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Host=db;Database=saas;Username=saas;Password=..." \
  -e Jwt__SigningKey="<votre-clé-32-chars>" \
  <registry>/<app>:<tag>
```

> L'image expose le port **8080** (HTTP). Le HTTPS est terminé au niveau du reverse-proxy/ingress.

---

## Variables d'environnement / Secrets (production)

Les variables marquées **Required** sont validées au démarrage (`builder.Configuration.GetRequired*`) — l'application refuse de démarrer si elles sont absentes ou vides. Utilisez un gestionnaire de secrets (Azure Key Vault, AWS Secrets Manager, Kubernetes Secrets, etc.) — **ne jamais committer ces valeurs**.

| Variable | Obligatoire | Description |
|---|---|---|
| `ConnectionStrings__Default` | **Required** | DSN Postgres (Npgsql) — ex. `Host=db;Database=saas;Username=app;Password=...` |
| `Jwt__SigningKey` | **Required** | Clé HMAC-SHA256 pour signer les JWT (≥ 32 caractères, aléatoire) |
| `Email__Host` | **Required** | Hôte SMTP (ex. `smtp.sendgrid.net`) |
| `Email__Port` | **Required** | Port SMTP (ex. `587` StartTLS, `465` SSL, `25` no-TLS) |
| `Email__FromAddress` | **Required** | Adresse expéditeur (ex. `no-reply@example.com`) |
| `Email__UseStartTls` | Optionnel | `true` pour StartTLS (port 587) |
| `Email__UseSslOnConnect` | Optionnel | `true` pour TLS implicite (port 465) |
| `Email__Username` | Optionnel | Identifiant SMTP si auth requise |
| `Email__Password` | Optionnel | Mot de passe SMTP — secret |
| `App__BaseUrl` | **Required** | URL publique du front (liens dans les emails — ex. `https://app.example.com`) |
| `Cors__AllowedOrigins` | **Required** | Origines CORS autorisées (JSON array — ex. `["https://app.example.com"]`) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optionnel | Endpoint OTLP (ex. `https://otel-collector:4317`) |
| `OTEL_SERVICE_NAME` | Optionnel | Nom du service pour les traces/métriques (défaut : `saas-api`) |
| `DATABASE_READONLY_URL` | Optionnel | DSN Postgres lecture seule pour le MCP dbhub — pointer un rôle sans droits d'écriture |
| `RateLimiting__DisableAuthPolicy` | **Ne pas activer** | Désactive le rate-limiter `/auth` — **doit rester `false` en production** (honoré uniquement en Development) |

### Variables optionnelles (valeurs par défaut)

| Variable | Défaut | Note |
|---|---|---|
| `Jwt__Issuer` | `saas-api` | Issuer des JWT |
| `Jwt__Audience` | `saas-clients` | Audience des JWT |
| `Jwt__AccessTokenMinutes` | `15` | Durée de vie du token d'accès |
| `Jwt__RefreshTokenDays` | `7` | Durée de vie du refresh token |

---

## Migrations de base de données

### Développement (`dotnet run`)

En environnement `Development` avec `Database:MigrateOnStartup=true` (valeur par défaut), l'API applique automatiquement les migrations au démarrage via `CreateExecutionStrategy().ExecuteAsync(() => MigrateAsync())`.

**Ne pas activer ce comportement en production** (risque de race condition en multi-instance).

### Production — Migration Bundle

Utiliser le **migration bundle** : un exécutable standalone qui applique les migrations idempotent-ement, à exécuter **une seule fois par release, avant le déploiement de la nouvelle version**.

```bash
# Générer le bundle (dans l'étape CI/CD, après build)
dotnet ef migrations bundle \
  --project src/Infrastructure \
  --startup-project src/Api \
  -o efbundle

# Appliquer les migrations (étape CI/CD AVANT le déploiement de l'app)
./efbundle --connection "$DATABASE_URL"
```

**Points clés :**
- Le bundle est idempotent (skipe les migrations déjà appliquées).
- À exécuter **avant** la mise en prod de la nouvelle image (pour que le schéma soit prêt).
- En multi-instance (plusieurs pods/conteneurs), un seul worker doit exécuter le bundle.
- Ne jamais activer `Database:MigrateOnStartup=true` en production (multi-instance).

---

## Health probes

L'API expose trois endpoints (tous anonymes) :

| Endpoint | Usage | Checks inclus |
|---|---|---|
| `/health/live` | **Liveness** — process alive | Aucun (réponse `Healthy` si le processus répond) |
| `/health/ready` | **Readiness** — dépendances OK | DB (tag `"ready"`) |
| `/health` | Tous les checks (commodité) | DB |

### Exemple Kubernetes

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
  failureThreshold: 3

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 5
  failureThreshold: 3
```

---

## Scaling & caveats

### Outbox (poll single-instance)

Le `OutboxProcessor` (background service) interroge la table `OutboxMessages` en boucle. En déploiement **multi-instance (plusieurs pods)**, plusieurs workers peuvent traiter les mêmes messages simultanément.

**Options :**
- Dédier un seul pod au worker Outbox (flag feature/env var).
- Ajouter `SELECT ... FOR UPDATE SKIP LOCKED` dans la requête Outbox pour une déduplication côté base (voir Plan 9 / ADR futur).

### Hangfire Dashboard

Le dashboard `/hangfire` est protégé par `HangfireDashboardAuthFilter`. En production :
- Restreindre l'accès aux utilisateurs avec le rôle `PlatformAdmin` (ou équivalent).
- Ou désactiver le dashboard en prod et utiliser un worker Hangfire séparé sans UI.

### HybridCache — multi-instance

Le `HybridCache` utilise le cache mémoire L1 (in-process). En multi-instance, les caches en mémoire ne se synchronisent pas.

**Solution :** Ajouter un backend Redis comme cache L2 :

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);
// HybridCache utilisera Redis comme L2 automatiquement
```

### HTTPS / TLS

La terminaison TLS est gérée au niveau du **reverse-proxy / ingress** (nginx, Traefik, ALB, Ingress Controller).

- `HSTS` et `HttpsRedirection` sont actifs hors `Development` (cf. `Program.cs`).
- En production, configurer le reverse-proxy pour forcer HTTPS et transmettre les headers `X-Forwarded-*` (`UseForwardedHeaders` si nécessaire selon la plateforme).
