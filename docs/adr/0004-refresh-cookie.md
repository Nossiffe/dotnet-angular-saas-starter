# ADR 0004 — Refresh token en cookie HttpOnly, pas d'antiforgery

- **Statut** : Accepté (2026-07-13)
- **Contexte** : le refresh token circulait dans le body JSON (`AuthResponse.RefreshToken`)
  et vivait en `localStorage` côté fronts — accessible à tout script exécuté dans la page
  (XSS). Le chantier sécurité migre son transport vers un cookie posé par l'API, jamais lu
  par le JS applicatif.

## Décision

Le refresh token est transporté par un cookie nommé `rt` (`Saas.Api.Common.RefreshCookie`),
posé/lu **uniquement** par les endpoints `/auth/*` :

| Attribut | Valeur | Pourquoi |
|---|---|---|
| `HttpOnly` | `true` | Inaccessible à `document.cookie` — élimine le vol par XSS (le risque que la migration adresse). |
| `Secure` | `true` (toujours, y compris en dev via HTTPS local) | Le cookie ne quitte jamais le canal chiffré ; pas de dérogation dev qui se retrouverait oubliée en prod. |
| `SameSite` | `Strict` | Le cookie n'est envoyé que sur une navigation/require **même-site** — bloque le CSRF cross-site par construction (un formulaire ou un script sur un autre site ne peut pas déclencher un envoi du cookie). |
| `Path` | `/api/v1/auth` | Le cookie ne circule sur AUCUNE autre route de l'API (surface d'attaque minimale, pas de fuite vers `/products`, `/organizations`, etc.). |
| `MaxAge` | `Jwt:RefreshTokenDays` | Aligné sur la durée de vie métier existante du refresh token. |

`AuthResponse` perd son champ `RefreshToken` ; `LogoutRequest` disparaît ; `/auth/refresh`
et `/auth/logout` n'ont plus de body — le cookie est la seule source de vérité pour la
rotation/révocation (mécanique `RefreshTokenStore` inchangée, seul le **transport** change).

## Pourquoi pas d'antiforgery (anti-CSRF token classique)

Un token antiforgery (double-submit cookie, synchronizer token pattern) est l'outil standard
quand un cookie est envoyé automatiquement **cross-site**. Ici, trois garanties cumulées
rendent ce mécanisme redondant :

1. **Même-origine** sur les deux fronts (§ ci-dessous) — le navigateur n'a jamais l'occasion
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
distribuer le token, header à propager côté fronts) — complexité non justifiée ici (YAGNI).
Si une des trois garanties venait à sauter (ex. un `SameSite=None` requis demain), cette
décision serait à rouvrir.

## Pourquoi les deux fronts passent même-origine

- **Angular** est déjà servi via son proxy dev (`web-angular/src/proxy.conf.json`) et, en
  prod, via le même reverse-proxy/domaine que l'API (`/api/*` réécrit vers le backend) —
  aucun changement requis.
- **React** utilisait `VITE_API_URL` pointant directement le backend sur un port distinct
  (cross-origin en dev) — il bascule sur le proxy Vite (`vite.config.ts`, `proxy: { '/api':
  ... }`), alignant son comportement sur Angular. `VITE_API_URL` disparaît : la variable
  n'a plus de raison d'exister une fois les deux fronts même-origine.

Sans cet alignement, React aurait eu besoin de `SameSite=None` pour son propre trafic
cross-origin — ce qui aurait cassé l'argument « pas d'antiforgery » ci-dessus pour LES DEUX
fronts (le cookie SameSite est une politique par cookie, pas par client).

## Exigence en production

Même-origine SPA/API est une **exigence de déploiement**, pas seulement une commodité dev :
le reverse-proxy de production doit servir le front et l'API sous le **même schéma+host**
(ex. `app.example.com` avec `/api/*` réécrit vers le backend), sinon le cookie `SameSite=Strict`
ne sera jamais envoyé vers l'API et le refresh échouera silencieusement. Documenté dans
`DEPLOYMENT.md` § cookie/même-site.

## Alternatives écartées

- **BFF (Backend-For-Frontend) complet** — un serveur intermédiaire par front qui détiendrait
  la session et proxy-erait vers l'API. Résout le même problème (cookie jamais exposé au SPA)
  mais ajoute un service déployable de plus par front, une source d'état de session
  supplémentaire, et une latence réseau additionnelle — disproportionné : le problème (vol du
  refresh token par XSS) est déjà résolu par `HttpOnly` seul, sans introduire de nouveau
  composant d'infra.
- **`SameSite=None` + antiforgery** — permettrait un déploiement cross-origin sans faire
  converger les fronts, mais réintroduit exactement la surface CSRF que `Strict` élimine, et
  nécessite alors un antiforgery token complet (endpoint de distribution + propagation header)
  pour compenser — plus de complexité pour un besoin (cross-origin) que l'alignement des
  fronts sur le proxy rend inutile ici.
- **Refresh token toujours en body JSON, stocké en mémoire (pas `localStorage`)** — évite le
  `localStorage` mais oblige à re-authentifier à chaque rechargement d'onglet (pas de
  persistance) ou à réinventer un stockage côté client aussi exposé au XSS que
  `localStorage` pour peu qu'un script tourne au moment critique — ne règle pas le problème
  XSS de fond que le cookie `HttpOnly` élimine par construction.
