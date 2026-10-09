# ADR 0004 — Refresh token en cookie HttpOnly, pas d'antiforgery

- **Statut** : Accepté (2026-07-13)
- **Contexte** : le refresh token circulait dans le body JSON (`AuthResponse.RefreshToken`)
  et vivait en `localStorage` côté front — accessible à tout script exécuté dans la page
  (XSS). Il passe dans un cookie posé par l'API, jamais lu par le JS applicatif.

## Décision

Le refresh token est transporté par un cookie nommé `rt` (`Saas.Api.Common.RefreshCookie`),
posé/lu **uniquement** par les endpoints `/auth/*` :

| Attribut | Valeur | Pourquoi |
|---|---|---|
| `HttpOnly` | `true` | Inaccessible à `document.cookie` — élimine le vol par XSS. |
| `Secure` | `true` (toujours, y compris en dev via HTTPS local) | Le cookie ne quitte jamais le canal chiffré ; pas de dérogation dev qui se retrouverait oubliée en prod. |
| `SameSite` | `Strict` | Le cookie n'est envoyé que sur une navigation ou une requête **même-site** — bloque le CSRF cross-site par construction (un formulaire ou un script sur un autre site ne peut pas déclencher un envoi du cookie). |
| `Path` | `/api/v1/auth` | Le cookie ne circule sur aucune autre route de l'API (surface d'attaque minimale, pas de fuite vers `/products`, `/organizations`, etc.). |
| `MaxAge` | `Jwt:RefreshTokenDays` | Aligné sur la durée de vie métier existante du refresh token. |

`AuthResponse` ne contient pas le refresh token, et `/auth/refresh` et `/auth/logout` n'ont pas
de body : le cookie est la seule source de vérité pour la rotation et la révocation
(`RefreshTokenStore`).

## Pourquoi pas d'antiforgery (anti-CSRF token classique)

Un token antiforgery (double-submit cookie, synchronizer token pattern) est l'outil standard
quand un cookie est envoyé automatiquement **cross-site**. Ici, trois garanties cumulées
rendent ce mécanisme redondant :

1. **Même-origine** entre le front et l'API (§ ci-dessous) — le navigateur n'a jamais l'occasion
   d'envoyer le cookie vers un tiers, ni un tiers de le faire envoyer vers l'API via un autre
   domaine : il n'y a pas de frontière cross-site à protéger côté navigation normale.
2. **`SameSite=Strict`** — même en cas d'erreur de config CORS, `Strict` empêche déjà l'envoi
   du cookie sur une requête initiée depuis un autre site (y compris navigation top-level
   depuis un lien externe, plus strict que `Lax`).
3. **POST-only + CORS explicite** — les endpoints qui consomment le cookie (`/auth/refresh`,
   `/auth/logout`) sont des `POST` (jamais déclenchables par un simple `<img>`/`<link>` GET
   cross-site), et `Cors:AllowedOrigins` est une liste explicite avec `AllowCredentials()` —
   aucune origine non listée ne peut faire une requête credentialed contre l'API, même avec
   un `SameSite` plus permissif.

Ajouter un token antiforgery par-dessus ces trois couches apporterait une redondance sans
réduire de risque réel identifié, au prix d'un aller-retour supplémentaire (endpoint pour
distribuer le token, header à propager côté front) — complexité non justifiée ici (YAGNI).
Si une des trois garanties venait à sauter (ex. un `SameSite=None` requis demain), cette
décision serait à rouvrir.

## Comment le front reste même-origine

Le front Angular passe par son proxy en développement (`web-angular/src/proxy.conf.json`) et,
en production, par le même reverse-proxy/domaine que l'API (`/api/*` réécrit vers le
backend). Il n'appelle jamais l'API sur un autre port ou un autre domaine.

Un client ajouté plus tard devra suivre la même règle : s'il appelait l'API en cross-origin,
il faudrait `SameSite=None`, et l'argument « pas d'antiforgery » ci-dessus tomberait pour tous
les clients (`SameSite` est une politique du cookie, pas du client).

## Exigence en production

Même-origine SPA/API est une **exigence de déploiement**, pas seulement une commodité dev :
le reverse-proxy de production doit servir le front et l'API sous le **même schéma+host**
(ex. `app.example.com` avec `/api/*` réécrit vers le backend), sinon le cookie `SameSite=Strict`
ne sera jamais envoyé vers l'API et le refresh échouera silencieusement. Documenté dans
`DEPLOYMENT.md` § cookie/même-site.

## Limites documentées

- **`SameSite=Strict` est same-SITE, pas same-ORIGIN.** L'hypothèse implicite ci-dessus
  (« pas de frontière cross-site à protéger ») repose sur des **sous-domaines maîtrisés** : un
  cookie `SameSite=Strict` posé par `app.example.com` est aussi envoyé pour une requête initiée
  depuis n'importe quel `*.example.com`. Un sous-domaine compromis (ex. XSS sur un site vitrine
  ou une preview d'environnement partageant le domaine enregistrable) pourrait donc déclencher
  des `POST /auth/refresh` ou `/auth/logout` — nuisance (rotation forcée, déconnexion) mais
  **sans lecture possible** (la réponse va au domaine d'origine réel, pas à l'attaquant) : pas
  d'exfiltration du refresh token via ce vecteur.
- **`Path=/api/v1/auth` est codé en dur** dans `Saas.Api.Common.RefreshCookie`, pas dérivé de la
  version d'API courante. Une future v2 (`/api/v2/auth/...`) doit soit **garder les routes
  d'authentification en v1** (le cookie continue de matcher), soit **ré-émettre explicitement le
  cookie** avec le nouveau `Path` au moment de la bascule — sinon le cookie posé par une v1
  antérieure ne sera jamais envoyé sur les routes v2 (le refresh échouera silencieusement pour
  les clients déjà authentifiés). Voir `Saas.Api.Common.RefreshCookie` au moment de cette bascule.

## Logout — `AllowAnonymous`

`/auth/logout` est délibérément `AllowAnonymous` (pas de `RequireAuthorization()`) : la
credential réelle du logout est la **possession** du cookie HttpOnly `rt`, pas l'access token
Bearer. Un access token expiré ou absent ne doit pas empêcher un utilisateur de se déconnecter ;
la révocation est idempotente (rien à révoquer si pas de cookie → 204 quand même) et la
suppression de cookie est sans effet de bord. Le logout-CSRF que `RequireAuthorization()`
aurait empêché reste couvert par `SameSite=Strict` (§ ci-dessus) — au pire une nuisance
(déconnexion forcée), jamais une fuite de données. Voir `AuthEndpoints.cs`.

## Alternatives écartées

- **BFF (Backend-For-Frontend) complet** — un serveur intermédiaire devant le front, qui détiendrait
  la session et proxy-erait vers l'API. Résout le même problème (cookie jamais exposé au SPA)
  mais ajoute un service déployable de plus, une source d'état de session
  supplémentaire, et une latence réseau additionnelle — disproportionné : le problème (vol du
  refresh token par XSS) est déjà résolu par `HttpOnly` seul, sans introduire de nouveau
  composant d'infra.
- **`SameSite=None` + antiforgery** — permettrait un déploiement cross-origin, mais réintroduit exactement la surface CSRF que `Strict` élimine, et
  nécessite alors un antiforgery token complet (endpoint de distribution + propagation header)
  pour compenser — plus de complexité pour un besoin (cross-origin) que le passage du front
  par le proxy rend inutile ici.
- **Refresh token toujours en body JSON, stocké en mémoire (pas `localStorage`)** — évite le
  `localStorage` mais oblige à re-authentifier à chaque rechargement d'onglet (pas de
  persistance) ou à réinventer un stockage côté client aussi exposé au XSS que
  `localStorage` pour peu qu'un script tourne au moment critique — ne règle pas le problème
  XSS de fond que le cookie `HttpOnly` élimine par construction.
