#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$ROOT"

echo "▶ build + tests backend"
dotnet build
docker network create devnet 2>/dev/null || true
dotnet test

echo "▶ check web-angular"
(cd web-angular && npm run check)
echo "▶ tests unitaires web-angular"
(cd web-angular && npm test)

echo "✓ check complet OK"
