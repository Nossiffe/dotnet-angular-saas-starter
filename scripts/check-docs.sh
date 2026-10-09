#!/usr/bin/env bash
# check-docs.sh — doc-lint heuristique
#
# Vérifie que les docs du repo ne contiennent pas de claims périmés :
#   1. Chemins cités en inline-code qui n'existent plus sur le disque
#   2. Port API citant un port ≠ launchSettings.json
#   3. Routes /api/v1/... qui ne correspondent à aucun endpoint défini
#
# Priorité : aucun faux positif, quitte à laisser passer des cas. Tolérer les placeholders/globs.
# Exit 0 si aucune violation, exit 1 si ≥1 violation.

set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
VIOLATIONS=0
FILES_SCANNED=0

# ---------------------------------------------------------------------------
# Fichiers à scanner
# ---------------------------------------------------------------------------
DOC_FILES=()
while IFS= read -r -d '' f; do
  DOC_FILES+=("$f")
done < <(
  find "$REPO" -maxdepth 1 \
    \( -name "README.md" \
       -o -name "CONTRIBUTING.md" -o -name "ARCHITECTURE.md" \
       -o -name "DEPLOYMENT.md" -o -name "CHANGELOG.md" \) \
    -print0
  find "$REPO/docs" "$REPO/security" -name "*.md" -print0
  find "$REPO/web-angular" -maxdepth 1 -name "README.md" -print0
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
violation() {
  echo "  VIOLATION: $*"
  VIOLATIONS=$((VIOLATIONS + 1))
}

# Vrai si le token contient un caractère placeholder/glob → ne pas vérifier
is_placeholder() {
  local token="$1"
  # Tolérer les caractères spéciaux glob/placeholder
  [[ "$token" == *'*'* ]] && return 0
  [[ "$token" == *'<'* ]] && return 0
  [[ "$token" == *'>'* ]] && return 0
  [[ "$token" == *'{'* ]] && return 0
  [[ "$token" == *'}'* ]] && return 0
  [[ "$token" == *'$'* ]] && return 0
  # Tolérer les noms de fichiers génériques de recettes/exemples (Foo*, Bar* dans src/...)
  # Ces noms sont des placeholders conventionnels dans les ADR et la doc — jamais des vrais fichiers.
  local leaf
  leaf="$(basename "$token")"
  [[ "$leaf" == Foo* ]] && return 0
  [[ "$leaf" == Bar* ]] && return 0
  # Placeholder numérique (ex. `docs/adr/NNNN` = "prochain numéro d'ADR", jamais un fichier réel).
  [[ "$leaf" == "NNNN" ]] && return 0
  return 1
}

# Chemins connus comme intentionnellement absents (optionnels, à créer par l'utilisateur)
# Clé = chemin repo-relatif exact
is_known_optional() {
  local path="$1"
  case "$path" in
    # Bundles de build front (gitignorés) — n'existent qu'après `npm run build`.
    "web-angular/dist/") return 0 ;;
  esac
  return 1
}

# Pour les ADR-style : docs/adr/0001 → chercher un fichier commençant par docs/adr/0001
path_exists_or_prefix() {
  local path="$1"
  # Chemins générés (gitignorés) : légitimement absents d'un checkout frais (CI).
  case "$path" in
    web-angular/src/app/api/generated*)
      return 0 ;;
  esac
  # Vérification exacte d'abord
  if [[ -e "$REPO/$path" ]]; then
    return 0
  fi
  # Vérification avec préfixe (pour les ADR courts type docs/adr/0001)
  local parent
  parent="$(dirname "$REPO/$path")"
  local basename
  basename="$(basename "$path")"
  if [[ -d "$parent" ]] && compgen -G "$parent/${basename}*" > /dev/null 2>&1; then
    return 0
  fi
  return 1
}

# ---------------------------------------------------------------------------
# CHECK 1 — Chemins inline-code
# ---------------------------------------------------------------------------
# Regex : token entre backticks commençant par un des préfixes connus
# On exclut les chemins frontend-relatifs courts (src/app/...)
# qui apparaissent dans la doc du front (web-angular/README.md)
# et représentent des chemins relatifs au dossier front, pas à la racine repo.
#
# Critère d'exclusion frontend-relatif :
#   - commence par "src/" ET le 2e segment n'est pas un dossier backend connu
#     (Api, Application, Domain, Infrastructure, tests)
BACKEND_SEGMENTS="Api|Application|Domain|Infrastructure|tests"

check_paths() {
  local file="$1"
  local rel_file="${file#"$REPO/"}"

  # Extraire tous les tokens inline-code du fichier
  # Pattern : `chemin/avec/slashes` entre backticks
  local token
  while IFS= read -r token; do
    [[ -z "$token" ]] && continue

    # Ignorer si contient un placeholder
    is_placeholder "$token" && continue

    # Ignorer les chemins frontend-relatifs courts (sans web-angular/ en préfixe)
    # ex: src/app/ cité dans la doc du front
    if [[ "$token" =~ ^src/ ]]; then
      local second_seg
      second_seg="${token#src/}"
      second_seg="${second_seg%%/*}"
      if ! [[ "$second_seg" =~ ^(Api|Application|Domain|Infrastructure|tests)$ ]]; then
        continue
      fi
    fi

    # Ignorer les chemins connus comme optionnels
    is_known_optional "$token" && continue

    # Vérifier l'existence (exacte ou par préfixe pour les ADR)
    if ! path_exists_or_prefix "$token"; then
      echo "  [paths] $rel_file : \`$token\` non trouvé sur le disque"
      VIOLATIONS=$((VIOLATIONS + 1))
    fi
  done < <(
    grep -oP '`\K(src|web-angular|scripts|docs|\.github)/[A-Za-z0-9._/-]+(?=`)' "$file" 2>/dev/null || true
  )
}

# ---------------------------------------------------------------------------
# CHECK 2 — Port API
# ---------------------------------------------------------------------------
LAUNCH_SETTINGS="$REPO/src/Api/Properties/launchSettings.json"
REAL_API_PORT=""

if [[ -f "$LAUNCH_SETTINGS" ]]; then
  # Extraire le port HTTP de applicationUrl (ex. "http://localhost:5123")
  REAL_API_PORT=$(grep -oP '"applicationUrl"\s*:\s*"http://localhost:\K[0-9]+' "$LAUNCH_SETTINGS" | head -1)
fi

check_api_port() {
  local file="$1"
  local rel_file="${file#"$REPO/"}"
  [[ -z "$REAL_API_PORT" ]] && return

  # Chercher tout localhost:NNNN dans le fichier
  local port_match
  while IFS= read -r port_match; do
    [[ -z "$port_match" ]] && continue
    # Ignorer si c'est le bon port
    [[ "$port_match" == "$REAL_API_PORT" ]] && continue
    # Ignorer les ports clairement non-API (Angular 4200, Adminer 8080, Mailpit 8025, Aspire 18888, HTTPS local 7100)
    case "$port_match" in
      4200|8080|8025|18888|587|465|25|4317|7100) continue ;;
    esac
    # Signaler comme port API suspect
    echo "  [port-api] $rel_file : localhost:$port_match cité (port API réel = $REAL_API_PORT)"
    VIOLATIONS=$((VIOLATIONS + 1))
  done < <(
    grep -oP '(?i)localhost:\K[0-9]+' "$file" 2>/dev/null || true
  )
}

# ---------------------------------------------------------------------------
# CHECK 3 — Endpoints /api/v1/...
# ---------------------------------------------------------------------------
# Vérifie le préfixe global /api/v1, puis que le premier segment de chaque route /api/v1/...
# citée dans la doc correspond à un groupe déclaré avec MapGroup dans src/Api/Endpoints/.

API_PREFIX_OK=false
if grep -qr '/api/v{version:apiVersion}\|/api/v1' "$REPO/src/Api/" 2>/dev/null; then
  API_PREFIX_OK=true
fi

# Groupes de routes déclarés avec MapGroup (/auth, /products...).
collect_endpoint_routes() {
  local routes=()

  while IFS= read -r line; do
    # MapGroup("/auth") → /auth
    if [[ "$line" =~ MapGroup\(\"(/[^\"]+)\"\) ]]; then
      routes+=("${BASH_REMATCH[1]}")
    fi
  done < <(grep -rh 'MapGroup' "$REPO/src/Api/Endpoints/" 2>/dev/null)

  # Retourner les groupes trouvés (auth, products...)
  for r in "${routes[@]}"; do
    echo "$r"
  done
}

KNOWN_GROUPS=()
while IFS= read -r g; do
  KNOWN_GROUPS+=("$g")
done < <(collect_endpoint_routes)

check_endpoints() {
  local file="$1"
  local rel_file="${file#"$REPO/"}"

  # Chercher les références /api/v1/... dans le fichier
  local ep
  while IFS= read -r ep; do
    [[ -z "$ep" ]] && continue

    # Ignorer si contient un placeholder
    is_placeholder "$ep" && continue

    # Vérifier que le préfixe /api/v1 est bien défini dans les sources
    if [[ "$API_PREFIX_OK" == false ]]; then
      echo "  [endpoints] Le préfixe /api/v1 n'a pas été trouvé dans src/Api/"
      VIOLATIONS=$((VIOLATIONS + 1))
      API_PREFIX_OK="reported"  # éviter de répéter
      continue
    fi

    # Extraire le segment après /api/v1/ (ex: auth/login → auth, products → products)
    local after_v1="${ep#/api/v1/}"
    # Premier segment = le groupe (auth, products...)
    local group_seg="/${after_v1%%/*}"

    local found=false
    for known in "${KNOWN_GROUPS[@]}"; do
      if [[ "$known" == "$group_seg" ]]; then
        found=true
        break
      fi
    done

    if [[ "$found" == false ]]; then
      echo "  [endpoints] $rel_file : \`$ep\` — groupe '$group_seg' introuvable dans src/Api/Endpoints/"
      VIOLATIONS=$((VIOLATIONS + 1))
    fi
  done < <(
    grep -oP '/api/v1/[A-Za-z0-9/_-]+' "$file" 2>/dev/null || true
  )
}

# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------
echo "check-docs.sh — scanning ${#DOC_FILES[@]} fichiers..."
echo ""

for file in "${DOC_FILES[@]}"; do
  [[ -f "$file" ]] || continue
  FILES_SCANNED=$((FILES_SCANNED + 1))
  rel="${file#"$REPO/"}"
  echo "Scanning: $rel"
  check_paths "$file"
  check_api_port "$file"
  check_endpoints "$file"
done

echo ""
echo "---"
echo "Fichiers scannés : $FILES_SCANNED"
echo "Violations       : $VIOLATIONS"
echo ""

if [[ "$VIOLATIONS" -gt 0 ]]; then
  echo "FAIL — $VIOLATIONS violation(s) trouvée(s)."
  exit 1
else
  echo "OK — aucune violation."
  exit 0
fi
