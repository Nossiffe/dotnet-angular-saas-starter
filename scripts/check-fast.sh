#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

cd "$ROOT"

dotnet build
dotnet format --verify-no-changes || {
  echo "Code non formaté — lance: dotnet format" >&2
  exit 2
}
