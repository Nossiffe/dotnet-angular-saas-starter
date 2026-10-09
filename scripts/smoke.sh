#!/usr/bin/env bash
# smoke.sh — Vérifie que l'API fonctionne end-to-end.
# Utilisation : ./scripts/smoke.sh [port]
# Prérequis : docker compose up -d db mailpit (ou déjà en cours), .env présent.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

PORT="${1:-5123}"
BASE="http://localhost:${PORT}"
EMAIL="smoke-$(date +%s)@saas.local"
PASSWORD="SmokeTest!99"
COOKIE_JAR="$(mktemp)"

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'
ok()   { echo -e "${GREEN}✓${NC} $1"; }
fail() { echo -e "${RED}✗${NC} $1"; exit 1; }

# ---- Ports depuis .env / docker-compose (ne pas hardcoder) ----
# On lit MAILPIT_UI_PORT depuis .env (défaut: 8025)
MAILPIT_PORT="${MAILPIT_UI_PORT:-8025}"
if [ -f "$ROOT/.env" ]; then
  # shellcheck disable=SC2046
  export $(grep -v '^#' "$ROOT/.env" | grep -v '^\s*$' | xargs) 2>/dev/null || true
  MAILPIT_PORT="${MAILPIT_UI_PORT:-8025}"
fi
MAILPIT="http://localhost:${MAILPIT_PORT}"

# ---- 1. Pré-requis ----
# En local, dotnet est fourni via mise ; en CI (setup-dotnet), mise n'existe pas.
if command -v mise >/dev/null 2>&1; then DOTNET_CMD=(mise exec -- dotnet); else DOTNET_CMD=(dotnet); fi
echo "▶ Démarrage de la base de données et de Mailpit"
docker compose up -d db mailpit
echo "▶ Migration EF"
"${DOTNET_CMD[@]}" ef database update --project src/Infrastructure --startup-project src/Api \
  --no-build 2>/dev/null || "${DOTNET_CMD[@]}" ef database update --project src/Infrastructure --startup-project src/Api

# ---- 2. Démarrer l'API en arrière-plan ----
echo "▶ Démarrage de l'API (port $PORT)"
ASPNETCORE_URLS="http://localhost:$PORT" \
ASPNETCORE_ENVIRONMENT=Development \
  "${DOTNET_CMD[@]}" run --project src/Api --no-build --no-launch-profile &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true; rm -f "$COOKIE_JAR"' EXIT

# Attendre que /health/ready réponde (vérifie aussi la DB)
for i in $(seq 1 30); do
  if curl -sf "${BASE}/health/ready" >/dev/null 2>&1; then break; fi
  echo "  attente démarrage ($i/30)..."
  sleep 2
done

# ---- 3. /health ----
echo "▶ Test /health"
STATUS=$(curl -sf "${BASE}/health")
echo "  health: $STATUS"
ok "/health répond"

# ---- Helper cookie refresh (rt) ----
# Lit la valeur du cookie 'rt' dans un cookie jar au format Netscape (curl -c).
# Retourne une chaîne vide si le cookie est absent (ex. après suppression par /auth/logout).
get_rt_cookie() {
  local jar="$1"
  awk -F'\t' '$6=="rt"{print $7}' "$jar" | tail -1
}

# ---- Helpers Mailpit ----
# Vider les messages Mailpit (état propre entre les tests)
mailpit_purge() {
  curl -sf -X DELETE "${MAILPIT}/api/v1/messages" >/dev/null
}

# Attendre qu'un email arrive pour un destinataire donné, retourne le premier ID de message.
# Usage: mailpit_wait_msg "email@example.com" [max_retries] [sleep_s]
mailpit_wait_msg() {
  local to_email="$1"
  local max="${2:-15}"
  local delay="${3:-1}"
  local encoded
  encoded=$(python3 -c "import urllib.parse; print(urllib.parse.quote('to:${to_email}'))")
  for _i in $(seq 1 "$max"); do
    local msg_id
    msg_id=$(curl -sf "${MAILPIT}/api/v1/search?query=${encoded}" \
      | python3 -c "import sys,json; msgs=json.load(sys.stdin)['messages']; print(msgs[0]['ID'] if msgs else '')")
    if [ -n "$msg_id" ]; then
      echo "$msg_id"
      return 0
    fi
    sleep "$delay"
  done
  return 1
}

# Récupérer le HTML d'un message par son ID
mailpit_body() {
  local msg_id="$1"
  curl -sf "${MAILPIT}/api/v1/message/${msg_id}" \
    | python3 -c "import sys,json; print(json.load(sys.stdin)['HTML'])"
}

# Extraire un paramètre d'URL depuis un corps HTML d'email
extract_param() {
  local body="$1"
  local param="$2"
  echo "$body" | python3 -c "
import sys, re
body = sys.stdin.read()
m = re.search(r'[?&]${param}=([^&\"\\s]+)', body)
print(m.group(1) if m else '')
"
}

# ---- 4a. Email-confirmed enforcement: register sans confirmer → login 400 ----
echo "▶ Email-confirmed enforcement"
EMAIL2="smoke-enforce-$(date +%s)@saas.local"
mailpit_purge

REGISTER_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL2}\",\"password\":\"${PASSWORD}\",\"organizationName\":\"Enforce Corp\",\"organizationSlug\":\"enforce-$(date +%s)\"}")
[ "$REGISTER_STATUS" = "201" ] || fail "Register (enforce): attendu 201, obtenu $REGISTER_STATUS"

# Login SANS confirmation → doit échouer avec 400
LOGIN_NO_CONFIRM_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL2}\",\"password\":\"${PASSWORD}\"}")
[ "$LOGIN_NO_CONFIRM_STATUS" = "400" ] || fail "Login sans confirmation: attendu 400, obtenu $LOGIN_NO_CONFIRM_STATUS"
ok "Login sans email confirmé → 400"

# Extraire le token de confirmation depuis Mailpit
MSG_ID=$(mailpit_wait_msg "$EMAIL2") || fail "Mailpit: email de confirmation non reçu pour ${EMAIL2}"
CONFIRM_BODY=$(mailpit_body "$MSG_ID")
CONFIRM_USER_ID=$(extract_param "$CONFIRM_BODY" "userId")
CONFIRM_TOKEN=$(extract_param "$CONFIRM_BODY" "token")
[ -n "$CONFIRM_USER_ID" ] || fail "Email confirmation: userId absent du lien"
[ -n "$CONFIRM_TOKEN" ] || fail "Email confirmation: token absent du lien"

# Confirmer l'email via l'API
CONFIRM_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/confirm-email" \
  -H "Content-Type: application/json" \
  -d "{\"userId\":\"${CONFIRM_USER_ID}\",\"token\":\"${CONFIRM_TOKEN}\"}")
[ "$CONFIRM_STATUS" = "200" ] || fail "confirm-email: attendu 200, obtenu $CONFIRM_STATUS"

# Login APRÈS confirmation → 200
LOGIN_CONFIRMED=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL2}\",\"password\":\"${PASSWORD}\"}")
[ "$LOGIN_CONFIRMED" = "200" ] || fail "Login après confirmation: attendu 200, obtenu $LOGIN_CONFIRMED"
ok "Login après confirmation email (via Mailpit) → 200"

# ---- 4. Register (utilisateur principal) ----
echo "▶ Register (utilisateur principal)"
mailpit_purge
REGISTER_BODY=$(cat <<BODY
{"email":"${EMAIL}","password":"${PASSWORD}","organizationName":"Smoke Corp","organizationSlug":"smoke-$(date +%s)"}
BODY
)
REGISTER_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "${REGISTER_BODY}")
[ "$REGISTER_STATUS" = "201" ] || fail "Register: attendu 201, obtenu $REGISTER_STATUS"
ok "POST /api/v1/auth/register → 201"

# ---- 4b. Confirmer l'email (direct en base pour l'utilisateur principal) ----
# Le login exige un email confirmé. En production, l'utilisateur clique le lien reçu
# par email (Mailpit en dev). Pour le flux principal on confirme directement en base afin de
# garder le test rapide et déterministe.
echo "▶ Confirmation de l'email (directe en base)"
docker compose exec -T db psql -U saas -d saas \
  -c "UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = true WHERE \"Email\" = '${EMAIL}';" >/dev/null \
  || fail "Confirmation email: échec UPDATE en base"
ok "Email confirmé en base pour ${EMAIL}"

# ---- 5. Login → JWT + cookie rt (HttpOnly) ----
echo "▶ Login"
LOGIN_RESPONSE=$(curl -sf \
  -X POST "${BASE}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -c "$COOKIE_JAR" -b "$COOKIE_JAR" \
  -d "{\"email\":\"${EMAIL}\",\"password\":\"${PASSWORD}\"}")
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
[ -n "$ACCESS_TOKEN" ] || fail "Login: accessToken absent"
echo "$LOGIN_RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); sys.exit(1 if 'refreshToken' in d else 0)" \
  || fail "Login: refreshToken présent dans le corps JSON (attendu : transport uniquement par cookie)"
RT_AFTER_LOGIN=$(get_rt_cookie "$COOKIE_JAR")
[ -n "$RT_AFTER_LOGIN" ] || fail "Login: cookie 'rt' absent du jar"
ok "POST /api/v1/auth/login → JWT obtenu, refreshToken absent du body, cookie 'rt' (HttpOnly) posé"

# ---- 5a. Refresh → 200 + rotation du cookie rt ----
echo "▶ Refresh (rotation du cookie rt)"
REFRESH_ROTATE_RESPONSE=$(curl -sf \
  -X POST "${BASE}/api/v1/auth/refresh" \
  -b "$COOKIE_JAR" -c "$COOKIE_JAR")
NEW_ACCESS_TOKEN=$(echo "$REFRESH_ROTATE_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
[ -n "$NEW_ACCESS_TOKEN" ] || fail "Refresh: accessToken absent"
RT_AFTER_REFRESH=$(get_rt_cookie "$COOKIE_JAR")
[ -n "$RT_AFTER_REFRESH" ] || fail "Refresh: cookie 'rt' absent du jar après refresh"
[ "$RT_AFTER_REFRESH" != "$RT_AFTER_LOGIN" ] || fail "Refresh: le cookie 'rt' n'a pas été renouvelé (rotation attendue)"
ACCESS_TOKEN="$NEW_ACCESS_TOKEN"
ok "POST /api/v1/auth/refresh → 200, nouveau accessToken + cookie 'rt' roté"

# ---- 5b. OpenAPI Bearer scheme ----
echo "▶ OpenAPI Bearer scheme"
OPENAPI_BODY=$(curl -sf "${BASE}/openapi/v1.json")
echo "$OPENAPI_BODY" | python3 -c "
import sys, json
d = json.load(sys.stdin)
schemes = d.get('components', {}).get('securitySchemes', {})
has_bearer = any(
  s.get('scheme', '').lower() == 'bearer' or s.get('type', '').lower() == 'http' and s.get('scheme', '').lower() == 'bearer'
  for s in schemes.values()
)
if not has_bearer:
    # Check using bearerFormat or name containing 'bearer'
    has_bearer = any('bearer' in str(s).lower() for s in schemes.values())
exit(0 if has_bearer else 1)
" || fail "OpenAPI /openapi/v1.json: aucun security scheme 'bearer' trouvé"
ok "GET /openapi/v1.json → security scheme 'bearer' présent"

# ---- 6. POST /api/v1/products (authentifié) ----
echo "▶ Créer un produit"
CREATE_RESPONSE=$(curl -sf \
  -X POST "${BASE}/api/v1/products" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -d '{"name":"Smoke Product","price":9.99}')
PRODUCT_ID=$(echo "$CREATE_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
[ -n "$PRODUCT_ID" ] || fail "Create product: id absent"
ok "POST /api/v1/products → 201 (id: $PRODUCT_ID)"

# ---- 7. GET /api/v1/products (authentifié, paginé) ----
echo "▶ Lister les produits (paginé)"
LIST_RESPONSE=$(curl -sf \
  -X GET "${BASE}/api/v1/products" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}")
COUNT=$(echo "$LIST_RESPONSE" | python3 -c "import sys,json; print(len(json.load(sys.stdin)['items']))")
[ "$COUNT" -ge 1 ] || fail "List products: liste vide ou champ 'items' absent"
ok "GET /api/v1/products → $COUNT produit(s) (paginé avec 'items')"

# ---- 7b. GET /api/v1/products/{id} ----
echo "▶ Obtenir un produit par ID"
GET_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X GET "${BASE}/api/v1/products/${PRODUCT_ID}" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}")
[ "$GET_STATUS" = "200" ] || fail "GET product by id: attendu 200, obtenu $GET_STATUS"
ok "GET /api/v1/products/${PRODUCT_ID} → 200"

# ---- 8. GET /api/v1/products sans token → 401 ----
echo "▶ GET /api/v1/products sans token (→ 401)"
UNAUTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${BASE}/api/v1/products")
[ "$UNAUTH_STATUS" = "401" ] || fail "Sans token: attendu 401, obtenu $UNAUTH_STATUS"
ok "GET /api/v1/products (sans token) → 401"

# ---- 9. 409 Concurrence : deux PUT parallèles sur le même produit ----
echo "▶ 409 Concurrence (deux PUT simultanés)"
GOT_409=0
# On répète quelques fois pour neutraliser l'aléa d'ordonnancement
for _attempt in $(seq 1 5); do
  OUT_A=$(mktemp) OUT_B=$(mktemp)
  curl -s -o "$OUT_A" -w "%{http_code}" \
    -X PUT "${BASE}/api/v1/products/${PRODUCT_ID}" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer ${ACCESS_TOKEN}" \
    -d "{\"name\":\"ConcA-${_attempt}\",\"price\":2.00}" > "${OUT_A}.code" &
  PID_A=$!
  curl -s -o "$OUT_B" -w "%{http_code}" \
    -X PUT "${BASE}/api/v1/products/${PRODUCT_ID}" \
    -H "Content-Type: application/json" \
    -H "Authorization: Bearer ${ACCESS_TOKEN}" \
    -d "{\"name\":\"ConcB-${_attempt}\",\"price\":3.00}" > "${OUT_B}.code" &
  PID_B=$!
  wait "$PID_A" "$PID_B"
  CODE_A=$(cat "${OUT_A}.code") CODE_B=$(cat "${OUT_B}.code")
  BODY_A=$(cat "$OUT_A") BODY_B=$(cat "$OUT_B")
  rm -f "$OUT_A" "$OUT_B" "${OUT_A}.code" "${OUT_B}.code"

  if [ "$CODE_A" = "409" ] || [ "$CODE_B" = "409" ]; then
    # Vérifier que le 409 contient traceId dans le body
    CONFLICT_BODY="$BODY_A"
    [ "$CODE_B" = "409" ] && CONFLICT_BODY="$BODY_B"
    HAS_TRACE=$(echo "$CONFLICT_BODY" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    print('yes' if 'traceId' in d else 'no')
except Exception:
    print('no')
")
    [ "$HAS_TRACE" = "yes" ] || fail "409 Concurrence: body ne contient pas 'traceId'"
    GOT_409=1
    break
  fi
done
[ "$GOT_409" = "1" ] || fail "409 Concurrence: aucun 409 obtenu après 5 tentatives"
ok "409 Concurrence → un 409 avec 'traceId' obtenu"

# ---- 10. Logout révoqué (cookie rt, sans body) ----
echo "▶ Logout → refresh révoqué (→ 401)"
LOGOUT_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/logout" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -b "$COOKIE_JAR" -c "$COOKIE_JAR")
[ "$LOGOUT_STATUS" = "204" ] || fail "Logout: attendu 204, obtenu $LOGOUT_STATUS"
RT_AFTER_LOGOUT=$(get_rt_cookie "$COOKIE_JAR")
[ -z "$RT_AFTER_LOGOUT" ] || fail "Logout: cookie 'rt' toujours présent dans le jar après logout"

REFRESH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/refresh" \
  -b "$COOKIE_JAR" -c "$COOKIE_JAR")
[ "$REFRESH_STATUS" = "401" ] || fail "Refresh après logout: attendu 401, obtenu $REFRESH_STATUS"
ok "Logout (204, cookie 'rt' supprimé du jar) → refresh sans cookie → 401"

# ---- 11. Forgot/Reset password ----
# Attendre la fin de la fenêtre du rate-limiter "auth" (FixedWindow 10/30s) : les étapes
# précédentes ont consommé ~9 jetons (register×2, login×3, confirm×1, refresh rotation, logout, refresh révoqué).
# Sans ce délai, forgot-password reçoit 429. 31s garantit que la fenêtre de 30s est expirée.
echo "▶ Attente reset fenêtre rate-limiter (31s)..."
sleep 31

echo "▶ Forgot-password (email connu) → email Mailpit → reset → login nouveau mdp"

# Nouvel utilisateur pour le forgot/reset
EMAIL3="smoke-reset-$(date +%s)@saas.local"
mailpit_purge

REG3_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL3}\",\"password\":\"${PASSWORD}\",\"organizationName\":\"Reset Corp\",\"organizationSlug\":\"reset-$(date +%s)\"}")
[ "$REG3_STATUS" = "201" ] || fail "Register (reset): attendu 201, obtenu $REG3_STATUS"

# Confirmer l'email via Mailpit
MSG3_CONFIRM=$(mailpit_wait_msg "$EMAIL3") || fail "Mailpit: email de confirmation non reçu pour ${EMAIL3}"
BODY3_CONFIRM=$(mailpit_body "$MSG3_CONFIRM")
UID3=$(extract_param "$BODY3_CONFIRM" "userId")
TOK3=$(extract_param "$BODY3_CONFIRM" "token")
[ -n "$UID3" ] && [ -n "$TOK3" ] || fail "Confirmation email: userId/token absents"
curl -sf -X POST "${BASE}/api/v1/auth/confirm-email" \
  -H "Content-Type: application/json" \
  -d "{\"userId\":\"${UID3}\",\"token\":\"${TOK3}\"}" >/dev/null

mailpit_purge

# Forgot-password (email connu) → 200
FORGOT_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/forgot-password" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL3}\"}")
[ "$FORGOT_STATUS" = "200" ] || fail "Forgot-password: attendu 200, obtenu $FORGOT_STATUS"

# Attendre que le job Hangfire ait traité la demande et envoyé l'email
MSG3_RESET=$(mailpit_wait_msg "$EMAIL3" 20 2) || fail "Mailpit: email de reset non reçu pour ${EMAIL3} (Hangfire trop lent ?)"
BODY3_RESET=$(mailpit_body "$MSG3_RESET")
RESET_UID=$(extract_param "$BODY3_RESET" "userId")
RESET_TOK=$(extract_param "$BODY3_RESET" "token")
[ -n "$RESET_UID" ] || fail "Reset email: userId absent du lien"
[ -n "$RESET_TOK" ] || fail "Reset email: token absent du lien"
ok "Forgot-password (email connu) → email Mailpit reçu avec userId+token"

NEW_PASSWORD="SmokeReset!99New"
RESET_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/reset-password" \
  -H "Content-Type: application/json" \
  -d "{\"userId\":\"${RESET_UID}\",\"token\":\"${RESET_TOK}\",\"newPassword\":\"${NEW_PASSWORD}\"}")
[ "$RESET_STATUS" = "200" ] || fail "Reset-password: attendu 200, obtenu $RESET_STATUS"

# Login avec nouveau mot de passe → 200
LOGIN_NEW=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL3}\",\"password\":\"${NEW_PASSWORD}\"}")
[ "$LOGIN_NEW" = "200" ] || fail "Login nouveau mdp: attendu 200, obtenu $LOGIN_NEW"
ok "Reset-password → login nouveau mot de passe → 200"

# ---- 11b. Forgot-password (email inconnu) → 200 sans email ----
echo "▶ Forgot-password (email inconnu) → 200 sans email"
UNKNOWN_EMAIL="nobody-smoke-$(date +%s)@saas.local"
mailpit_purge

FORGOT_UNKNOWN=$(curl -s -o /dev/null -w "%{http_code}" \
  -X POST "${BASE}/api/v1/auth/forgot-password" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${UNKNOWN_EMAIL}\"}")
[ "$FORGOT_UNKNOWN" = "200" ] || fail "Forgot inconnu: attendu 200, obtenu $FORGOT_UNKNOWN"

# Attendre un peu puis vérifier qu'aucun email n'est arrivé (anti-énumération)
sleep 3
MSG_COUNT=$(curl -sf "${MAILPIT}/api/v1/messages" \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['total'])")
[ "$MSG_COUNT" = "0" ] || fail "Forgot inconnu: un email a été envoyé alors qu'il ne devrait pas l'être (anti-énumération KO)"
ok "Forgot-password (email inconnu) → 200 + aucun email envoyé"

# ---- 12. Rate limiting ----
echo "▶ Rate limiting (12 POST rapides → au moins un 429)"
# Le rate limiter 'auth' : PermitLimit=10, Window=30s (FixedWindow).
# Note: les IPs loopback sont exemptées du GLOBAL limiter, mais le rate limiter
# 'auth' par endpoint est appliqué indépendamment (clé = IP, y compris loopback).
GOT_429=0
STATUSES=""
for _i in $(seq 1 12); do
  S=$(curl -s -o /dev/null -w "%{http_code}" \
    -X POST "${BASE}/api/v1/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"ratelimit-smoke-$(date +%s%N)@saas.local\",\"password\":\"WrongPass!\"}")
  STATUSES="$STATUSES $S"
  [ "$S" = "429" ] && GOT_429=1
done
echo "  statuts obtenus:${STATUSES}"
[ "$GOT_429" = "1" ] || fail "Rate-limit: aucun 429 obtenu en 12 requêtes rapides (statuts:${STATUSES})"
ok "Rate-limit → au moins un 429 obtenu sur 12 requêtes"

echo ""
echo -e "${GREEN}✓ Smoke test complet OK${NC}"
