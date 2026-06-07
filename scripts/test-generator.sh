#!/usr/bin/env bash
# =============================================================================
# test-generator.sh — CI test: generate both fronts, assert no residuals,
#                     locked build passes, all YAML/JSON valid.
# Usage: bash scripts/test-generator.sh
# =============================================================================
set -euo pipefail

GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
pass() { echo -e "${GREEN}  PASS${NC} $1"; }
fail() { echo -e "${RED}  FAIL${NC} $1"; FAILED=1; }
info() { echo -e "${BLUE}  --->${NC} $1"; }

# Locate repo root (script is in <repo>/scripts/)
REPO="$(cd "$(dirname "$0")/.." && pwd)"
GENERATOR="$REPO/tools/new-saas"

[ -x "$GENERATOR" ] || { echo -e "${RED}ERROR:${NC} $GENERATOR not found or not executable" >&2; exit 1; }

# Temp dir + cleanup trap
TMPDIR_ROOT="$(mktemp -d)"
cleanup() { rm -rf "$TMPDIR_ROOT"; }
trap cleanup EXIT

OVERALL=0

# =============================================================================
# run_front <front>
# =============================================================================
run_front() {
  local FRONT="$1"
  local NAME="zztest-${FRONT}"
  local DEST="$TMPDIR_ROOT/$NAME"
  local REMOVE

  echo ""
  echo -e "${YELLOW}========================================${NC}"
  echo -e "${YELLOW}  Front: $FRONT  →  $DEST${NC}"
  echo -e "${YELLOW}========================================${NC}"

  FAILED=0

  # Determine removed front
  if [ "$FRONT" = "angular" ]; then
    REMOVE="web-react"
  else
    REMOVE="web-angular"
  fi

  # --- Generate ---------------------------------------------------------------
  info "Generating with tools/new-saas..."
  TEMPLATE="$REPO" "$GENERATOR" "$NAME" \
    --front="$FRONT" \
    --dest="$DEST" \
    --no-ports \
    --no-install \
    --no-git \
    --no-repo

  # --- (a) Token residuals: Saas / saas (excluding project name) --------------
  info "Assertion (a): no 'Saas' / 'saas' token residuals (excluding project name)..."
  RESIDUALS=$(grep -rnI -e 'Saas' -e '\bsaas\b' "$DEST" \
    --exclude-dir=node_modules \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude-dir=dist \
    --exclude-dir=.git \
    2>/dev/null | grep -v "zztest-${FRONT}" | grep -v "Zztest${FRONT^}" | grep -v "zztest${FRONT}" || true)
  if [ -z "$RESIDUALS" ]; then
    pass "(a) No 'Saas'/'saas' residuals"
  else
    fail "(a) Found 'Saas'/'saas' residuals:"
    echo "$RESIDUALS" | head -20
  fi

  # --- (b) Removed front references -------------------------------------------
  # tools/ and scripts/ are meta-files (the generator itself + test harness) that
  # legitimately reference both front names as string literals in their logic.
  # generator.yml also legitimately lists both fronts in its path triggers.
  # Only assert on actual generated SaaS content.
  info "Assertion (b): no references to removed front '$REMOVE' (excl. tools/, scripts/, .github/workflows/generator.yml)..."
  FRONT_RESIDUALS=$(grep -rnI "$REMOVE" "$DEST" \
    --exclude-dir=node_modules \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude-dir=dist \
    --exclude-dir=.git \
    2>/dev/null \
    | grep -v "^$DEST/tools/" \
    | grep -v "^$DEST/scripts/" \
    | grep -v "^$DEST/.github/workflows/generator.yml:" \
    || true)
  if [ -z "$FRONT_RESIDUALS" ]; then
    pass "(b) No '$REMOVE' references (outside meta-files)"
  else
    fail "(b) Found '$REMOVE' references:"
    echo "$FRONT_RESIDUALS" | head -20
  fi

  # --- (c) Template port residuals: 5123 / 5001 --------------------------------
  # tools/new-saas contains sed patterns like s/5123/$API_PORT/ — those are the
  # replacement instructions, not residuals. Exclude tools/ and scripts/.
  info "Assertion (c): no template port residuals (5123, 5001) (excl. tools/, scripts/)..."
  PORT_RESIDUALS=$(grep -rnIE '\b(5123|5001)\b' "$DEST" \
    --exclude-dir=node_modules \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude-dir=dist \
    --exclude-dir=.git \
    2>/dev/null \
    | grep -v "^$DEST/tools/" \
    | grep -v "^$DEST/scripts/" \
    || true)
  if [ -z "$PORT_RESIDUALS" ]; then
    pass "(c) No template port residuals (5123/5001) outside meta-files"
  else
    fail "(c) Found template port residuals:"
    echo "$PORT_RESIDUALS" | head -20
  fi

  # --- (d) dotnet restore --locked-mode + dotnet build --no-restore -----------
  info "Assertion (d): dotnet restore --locked-mode && dotnet build --no-restore..."
  if ( cd "$DEST" && mise exec -- dotnet restore --locked-mode 2>&1 && mise exec -- dotnet build --no-restore 2>&1 ); then
    pass "(d) dotnet restore --locked-mode && dotnet build --no-restore: OK"
  else
    fail "(d) dotnet restore/build failed (see output above)"
  fi

  # --- (e) YAML validity -------------------------------------------------------
  info "Assertion (e): YAML files valid..."
  YAML_FAIL=0
  # Collect all YAML files to check
  YAML_FILES=()
  while IFS= read -r -d '' f; do
    YAML_FILES+=("$f")
  done < <(find "$DEST/.github" -name "*.yml" -print0 2>/dev/null || true)
  for f in "$DEST/docker-compose.yml" "$DEST/lefthook.yml" "$DEST/.github/dependabot.yml"; do
    [ -f "$f" ] && YAML_FILES+=("$f") || true
  done
  # Remove duplicates
  mapfile -t YAML_FILES < <(printf '%s\n' "${YAML_FILES[@]}" | sort -u)
  for f in "${YAML_FILES[@]}"; do
    if python3 -c "import yaml; yaml.safe_load(open('$f'))" 2>/dev/null; then
      pass "(e) YAML OK: ${f#"$DEST/"}"
    else
      fail "(e) YAML INVALID: ${f#"$DEST/"}"
      YAML_FAIL=1
    fi
  done

  # --- (e) JSON validity -------------------------------------------------------
  info "Assertion (e): JSON files valid..."
  JSON_FILES=()
  for f in \
    "$DEST/.claude/settings.json" \
    "$DEST/src/.claude/settings.json" \
    "$DEST/.devcontainer/devcontainer.json" \
    "$DEST/.mcp.json"; do
    [ -f "$f" ] && JSON_FILES+=("$f") || true
  done
  for f in "${JSON_FILES[@]}"; do
    if jq -e . "$f" >/dev/null 2>&1; then
      pass "(e) JSON OK: ${f#"$DEST/"}"
    else
      fail "(e) JSON INVALID: ${f#"$DEST/"}"
    fi
  done

  # --- Front summary ----------------------------------------------------------
  echo ""
  if [ "$FAILED" -eq 0 ]; then
    echo -e "${GREEN}  [PASS] Front: $FRONT — all assertions passed${NC}"
  else
    echo -e "${RED}  [FAIL] Front: $FRONT — one or more assertions failed${NC}"
    OVERALL=1
  fi
}

# =============================================================================
# Run for both fronts
# =============================================================================
run_front angular
run_front react

echo ""
echo -e "${YELLOW}========================================${NC}"
if [ "$OVERALL" -eq 0 ]; then
  echo -e "${GREEN}  ALL FRONTS PASSED${NC}"
else
  echo -e "${RED}  SOME FRONTS FAILED${NC}"
fi
echo -e "${YELLOW}========================================${NC}"

exit "$OVERALL"
