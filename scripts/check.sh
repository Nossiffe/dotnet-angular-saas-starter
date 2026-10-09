#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$ROOT"

echo "▶ build + tests backend"
dotnet build
docker network create devnet 2>/dev/null || true
dotnet test

for web in web-angular; do
  if [ -d "$web" ]; then
    echo "▶ check $web"
    (cd "$web" && npm run check)
    echo "▶ test unitaires $web"
    (cd "$web" && npm test)
  fi
done

echo "✓ check complet OK"
