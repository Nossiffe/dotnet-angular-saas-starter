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

## Release — image, SBOM, attestation de provenance

Le workflow `.github/workflows/release.yml` se déclenche sur un tag `v*` (ou manuellement) et,
pour chaque release :

1. **Build & push** l'image sur GHCR (`ghcr.io/<repo>`), taguée par semver + SHA court.
2. **SBOM** (Software Bill of Materials), généré avant l'attestation (voir note ci-dessous) :
   - Backend : `dotnet tool run dotnet-CycloneDX -- Saas.slnx -o sbom --filename sbom-backend.json --json`
     (format CycloneDX ; le `--` sépare les arguments de `dotnet tool run` de ceux passés à
     l'outil).
   - Front Angular : `npm sbom --sbom-format cyclonedx --omit dev` (SBOM natif npm, dépendances
     de prod uniquement).
   - Tous les fichiers `sbom/*.json` sont publiés en artifact du run (`actions/upload-artifact`).
3. **Attestation de provenance** (SLSA build L2, `actions/attest-build-provenance`) sur le
   digest de l'image poussée — preuve vérifiable que l'image provient bien de ce repo/run/commit,
   publiée sur le registre GHCR. Placée **après** les SBOM dans le workflow : sur un dépôt
   privé hors GitHub Enterprise, cette étape échoue (les attestations y exigent GitHub
   Enterprise Cloud) ; les SBOM des images déjà poussées restent alors publiés.

### Vérifier l'attestation d'une image

```bash
gh attestation verify oci://ghcr.io/<owner>/<repo>:<tag> --owner <owner>
```

Une vérification réussie confirme que l'image a été construite par le workflow `release.yml`
de ce dépôt (pas d'image substituée en amont du registre) — à faire avant tout déploiement
d'une image tirée d'un registre partagé, ou en gate d'un pipeline de déploiement séparé.

---

## Variables d'environnement / Secrets (production)

`appsettings.json` ne contient aucune valeur propre à un environnement : les valeurs de
développement (base locale, Mailpit, `http://localhost:4200`) sont dans
`appsettings.Development.json`, lu seulement quand `ASPNETCORE_ENVIRONMENT=Development`. Hors
Development, l'API refuse donc de démarrer si une variable **Required** manque : la chaîne de
connexion est contrôlée explicitement, les autres par la validation des options au démarrage
(`ValidateDataAnnotations().ValidateOnStart()`). Utilisez un gestionnaire de secrets (Azure Key
Vault, AWS Secrets Manager, Kubernetes Secrets, etc.) — **ne jamais committer ces valeurs**.

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
| `App__BaseUrl` | **Required** | URL publique du front, absolue (liens dans les emails — ex. `https://app.example.com`) |
| `Cors__AllowedOrigins__0`, `__1`… | Optionnel | Origines autorisées à appeler l'API depuis un autre domaine. Vide par défaut : le front est servi depuis la même origine que l'API (`docs/adr/0004`) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Optionnel | Endpoint OTLP (ex. `https://otel-collector:4317`) |
| `OTEL_SERVICE_NAME` | Optionnel | Nom du service pour les traces/métriques (défaut : `saas-api`) |
| `DataProtection__KeysPath` | Recommandé en prod | Répertoire du volume persistant pour les clés Data Protection — voir § dédiée ci-dessous |
| `ForwardedHeaders__Enabled` | Recommandé derrière un proxy | `true` si l'API est derrière un reverse-proxy/ingress qui termine TLS — voir § dédiée ci-dessous. ⚠️ Avec ce flag actif, l'API fait confiance à `X-Forwarded-*` : son port ne doit être joignable **que** depuis le réseau du proxy (jamais exposé publiquement), sinon un client forgerait son IP et fausserait le rate limiting |

### Variables optionnelles (valeurs par défaut)

| Variable | Défaut | Note |
|---|---|---|
| `Jwt__Issuer` | `saas-api` | Issuer des JWT |
| `Jwt__Audience` | `saas-clients` | Audience des JWT |
| `Jwt__AccessTokenMinutes` | `15` | Durée de vie du token d'accès |
| `Jwt__RefreshTokenDays` | `7` | Durée de vie du refresh token |
| `RequestTimeouts__DefaultSeconds` | `30` | Délai au-delà duquel une requête est coupée |

---

## Cookie refresh & exigence même-site (SPA ↔ API)

Le refresh token est transporté par un cookie `rt` `HttpOnly` + `Secure` + `SameSite=Strict`
+ `Path=/api/v1/auth` (décision complète : `docs/adr/0004-refresh-cookie.md`). `SameSite=Strict`
signifie que le navigateur **n'envoie le cookie que si le front et l'API partagent le même
site** (même schéma + même domaine enregistrable).

**Conséquence pour le déploiement** : le reverse-proxy/ingress de production doit servir le
SPA et l'API sous le **même host** (typiquement `app.example.com` avec `/api/*` réécrit vers
le backend `api` interne). Exemple nginx :

```nginx
server {
    listen 443 ssl;
    server_name app.example.com;

    location /api/ {
        proxy_pass http://api:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        root /usr/share/nginx/html; # build statique du SPA
        try_files $uri /index.html;
    }
}
```

Si un déploiement cross-origin (domaines distincts pour le SPA et l'API) est un jour
requis, `SameSite=Strict` ne fonctionnera plus (le cookie ne partira jamais) — il faudrait
alors rouvrir l'ADR 0004 (`SameSite=None` + antiforgery, ou BFF), pas juste changer la valeur
du cookie isolément.

---

## Data Protection — persistance des clés

Les endpoints Identity (confirmation d'email, reset de mot de passe) chiffrent leurs tokens
via **ASP.NET Core Data Protection**. Sans configuration explicite, les clés sont générées
**en mémoire du conteneur** : elles disparaissent à chaque redémarrage/redéploiement, ce qui
invalide silencieusement tous les tokens de confirmation/reset émis avant le redéploiement
(l'utilisateur reçoit une erreur « lien invalide » sur un lien pourtant récent).

**En production**, monter un volume persistant et pointer `DataProtection__KeysPath` dessus :

```yaml
services:
  api:
    volumes:
      - dataprotection-keys:/app/keys
    environment:
      DataProtection__KeysPath: /app/keys

volumes:
  dataprotection-keys:
```

En Kubernetes, un `PersistentVolumeClaim` monté au même chemin. **En multi-instance**, ce
volume doit être **partagé** entre les pods (ex. `ReadWriteMany`, ou un stockage réseau) —
sinon chaque instance génère ses propres clés et ne peut pas déchiffrer les tokens émis par
une autre instance.

**Avertissement (démarrage)** : par défaut, ASP.NET Core Data Protection persiste les clés
**en clair** sur le volume (aucun chiffrement au repos) — protéger l'accès au volume (permissions
fichier, chiffrement du stockage sous-jacent) comme n'importe quel secret. Pour une cible ASVS
Level 2, envisager `ProtectKeysWithCertificate` (chiffrement des clés elles-mêmes avec un
certificat X.509) plutôt que la persistance en clair par défaut.

---

## ForwardedHeaders derrière un reverse-proxy

Quand l'API tourne derrière un reverse-proxy/ingress qui termine TLS (nginx, Traefik, ALB,
Ingress Controller Kubernetes), Kestrel ne voit que la connexion **interne** (souvent HTTP,
IP du proxy) — sans réécriture, `Request.Scheme` et `Connection.RemoteIpAddress` sont faux
au niveau applicatif, ce qui casse `UseHsts`/`UseHttpsRedirection` (le HTTPS externe n'est
pas vu par ASP.NET Core) et le calcul du `Secure` du cookie refresh.

Activer explicitement :

```
ForwardedHeaders__Enabled=true
```

**Uniquement** quand un proxy de confiance est effectivement devant l'API (sinon un client
pourrait usurper `X-Forwarded-For`/`X-Forwarded-Proto` directement — surface d'attaque si
l'API est exposée sans proxy devant). Le middleware ne traite que `X-Forwarded-For` et
`X-Forwarded-Proto`.

Le middleware s'exécute avant `UseHsts` et `UseHttpsRedirection`, qui doivent voir le scheme du
client. Par défaut, ASP.NET Core n'accepte ces en-têtes que d'un proxy en loopback, et les ignore
sans erreur dès que le proxy est sur une autre machine (cas courant : nginx dans un conteneur
séparé, sur le réseau Docker). L'API vide donc la liste des proxys connus et n'accepte qu'un seul
saut (`ForwardLimit = 1`) : la confiance repose sur le fait que `ForwardedHeaders__Enabled` n'est
activé que derrière un proxy qui écrase ces en-têtes pour les requêtes externes. Si le port de
l'API restait joignable en direct, un client pourrait forger `X-Forwarded-For` et fausser le
partitionnement par IP de la limitation de débit.

---

## Headers de sécurité — API vs SPA statique

L'API applique déjà un preset de headers de sécurité (`NetEscapades.AspNetCore.SecurityHeaders`,
`AddDefaultApiSecurityHeaders()` dans `HttpPipelineExtensions`) — rien à faire côté backend.

Le **SPA statique** (Angular buildé, servi par nginx ou équivalent) a besoin de ses
propres headers — notamment une CSP stricte, différente de celle de l'API (elle doit
autoriser le runtime JS/CSS du bundle). Exemple nginx pour le hosting statique :

```nginx
add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
```

- `style-src 'unsafe-inline'` : concession usuelle pour Angular (styles de composants
  injectés inline) — à remplacer par des nonces (`ngCspNonce`) si un audit l'exige.
- `connect-src 'self'` fonctionne car le SPA et l'API sont même-origine (§ cookie ci-dessus)
  — pas besoin d'y lister un host d'API distinct.
- `frame-ancestors 'none'` : équivalent CSP de `X-Frame-Options: DENY`, redondant
  volontairement (défense en profondeur pour les navigateurs qui ne supportent que l'un
  des deux).

## Migrations de base de données

### Développement (`dotnet run`)

En environnement `Development` avec `Database:MigrateOnStartup=true` (valeur par défaut), l'API applique automatiquement les migrations au démarrage via `CreateExecutionStrategy().ExecuteAsync(() => MigrateAsync())` (`DatabaseStartupExtensions`). Les deux conditions sont requises ; les tests d'intégration désactivent la seconde, car ils migrent eux-mêmes leur base.

Sur une base vide, `MigrateAsync` lit d'abord la table d'historique, absente, et EF journalise cet échec attendu en erreur. L'API crée donc la base et la table d'historique avant de migrer, pour un premier lancement sans erreur dans les logs.

**Ne pas activer ce comportement en production** (risque de race condition en multi-instance).

### Production — Migration Bundle

Utiliser le **migration bundle** : un exécutable standalone qui applique les migrations idempotent-ement, à exécuter **une seule fois par release, avant le déploiement de la nouvelle version**.

```bash
# Générer le bundle (dans l'étape CI/CD, après build)
dotnet ef migrations bundle \
  --project src/Infrastructure \
  --startup-project src/Api \
  -o efbundle

# Appliquer les migrations (étape CI/CD, avant le déploiement de l'app)
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

### Outbox en multi-instance

Chaque instance de l'API fait tourner un `OutboxProcessor`. Chaque message est verrouillé avec
`FOR UPDATE SKIP LOCKED` le temps de son traitement : deux instances ne traitent jamais le même
message en même temps, sans configuration particulière. La livraison reste « au moins une fois »
(voir `ARCHITECTURE.md` § Transactional Outbox).

### Messages de l'outbox abandonnés

Après 5 tentatives, un message est abandonné : il reste dans `OutboxMessages` avec
`"ProcessedOn" IS NULL`, et l'API journalise l'erreur `Outbox : message {MessageId} abandonné`.
Une fois la cause corrigée (SMTP indisponible, par exemple), on le relance :

```sql
-- Messages abandonnés et leur dernière erreur
SELECT "Id", "Type", "OccurredOn", "Error" FROM "OutboxMessages"
WHERE "ProcessedOn" IS NULL AND "Attempts" >= 5;
-- Pour un e-mail, "Error" donne le type de l'erreur SMTP et ses codes, jamais l'adresse.

-- Relance : repris au lot suivant ; les handlers déjà passés ne sont pas rappelés
UPDATE "OutboxMessages" SET "Attempts" = 0, "NextAttemptOn" = NULL
WHERE "Id" = '<id>';
```

Les messages traités sont supprimés après 7 jours par le job Hangfire
`purge-processed-outbox-messages` ; les messages abandonnés ne sont jamais purgés.

### Hangfire Dashboard

Le dashboard `/hangfire` est protégé par `HangfireDashboardAuthFilter`. Hors Development, il
exige le claim `platform_role=PlatformAdmin`, qu'aucun flux de connexion n'émet : il faut le
provisionner pour un opérateur. Autre option : ne pas monter le dashboard en production.

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

- `HSTS` (`max-age` 730 jours, `includeSubDomains`) et `HttpsRedirection` sont actifs hors `Development` (`src/Api/Hosting/HttpPipelineExtensions.cs`). Le `preload` HSTS n'est **pas** activé par défaut (engagement quasi irréversible auprès des listes des navigateurs) : l'ajouter (`options.Preload = true` dans `AddHsts`) seulement après avoir confirmé que le domaine reste HTTPS partout durablement, puis le soumettre à hstspreload.org.
- En production, configurer le reverse-proxy pour forcer HTTPS et transmettre les headers `X-Forwarded-*`, et activer `ForwardedHeaders__Enabled=true` côté API (§ dédiée ci-dessus) pour qu'elle les interprète.
