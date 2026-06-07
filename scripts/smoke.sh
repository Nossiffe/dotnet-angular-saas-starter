#!/usr/bin/env bash
# smoke.sh — Vérifie que l'API fonctionne end-to-end.
# Utilisation : ./scripts/smoke.sh [port]
# Prérequis : docker compose up -d (Postgres), .env présent.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

PORT="${1:-5123}"
BASE="http://localhost:${PORT}"
EMAIL="smoke-$(date +%s)@saas.local"
PASSWORD="SmokeTest!99"

RED='\033[0;31m'; GREEN='\033[0;32m'; NC='\033[0m'
ok()   { echo -e "${GREEN}✓${NC} $1"; }
fail() { echo -e "${RED}✗${NC} $1"; exit 1; }

# ---- 1. Pré-requis ----
echo "▶ Démarrage de la base de données"
docker compose up -d db
echo "▶ Migration EF"
dotnet ef database update --project src/Infrastructure --startup-project src/Api \
  --no-build 2>/dev/null || dotnet ef database update --project src/Infrastructure --startup-project src/Api

# ---- 2. Démarrer l'API en arrière-plan ----
echo "▶ Démarrage de l'API (port $PORT)"
ASPNETCORE_URLS="http://localhost:$PORT" \
  dotnet run --project src/Api --no-build &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true' EXIT

# Attendre que l'API réponde
for i in $(seq 1 30); do
  if curl -sf "${BASE}/health" >/dev/null 2>&1; then break; fi
  echo "  attente démarrage ($i/30)..."
  sleep 2
done

# ---- 3. /health ----
echo "▶ Test /health"
STATUS=$(curl -sf "${BASE}/health")
echo "  health: $STATUS"
ok "/health répond"

# ---- 4. Register ----
echo "▶ Register"
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

# ---- 5. Login → JWT ----
echo "▶ Login"
LOGIN_RESPONSE=$(curl -sf \
  -X POST "${BASE}/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"${EMAIL}\",\"password\":\"${PASSWORD}\"}")
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
[ -n "$ACCESS_TOKEN" ] || fail "Login: accessToken absent"
ok "POST /api/v1/auth/login → JWT obtenu"

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

# ---- 7. GET /api/v1/products (authentifié) ----
echo "▶ Lister les produits"
LIST_RESPONSE=$(curl -sf \
  -X GET "${BASE}/api/v1/products" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}")
COUNT=$(echo "$LIST_RESPONSE" | python3 -c "import sys,json; print(len(json.load(sys.stdin)['items']))")
[ "$COUNT" -ge 1 ] || fail "List products: liste vide"
ok "GET /api/v1/products → $COUNT produit(s) (paginé)"

# ---- 8. GET /api/v1/products sans token → 401 ----
echo "▶ GET /api/v1/products sans token (→ 401)"
UNAUTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${BASE}/api/v1/products")
[ "$UNAUTH_STATUS" = "401" ] || fail "Sans token: attendu 401, obtenu $UNAUTH_STATUS"
ok "GET /api/v1/products (sans token) → 401"

echo ""
echo -e "${GREEN}✓ Smoke test complet OK${NC}"
