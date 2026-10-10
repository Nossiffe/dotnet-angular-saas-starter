#!/usr/bin/env bash
# pr-changes.sh — dit si une pull request modifie un fichier qui correspond à des motifs.
#
# Usage : scripts/pr-changes.sh <sortie> <motif>...
#
# Utilisé par le job `changes` des workflows dont les jobs sont des vérifications exigées
# (deps-audit, smoke, stryker, zizmor). Un workflow filtré par `paths` ne poste aucun statut
# quand il ne se déclenche pas, et la PR reste bloquée en attente de ce statut. Un job ignoré
# par son `if`, lui, compte comme réussi : ces workflows se déclenchent donc sur toutes les PR,
# et leurs jobs consultent la sortie de ce script.
#
# - Les motifs ont la syntaxe des filtres `paths` de GitHub Actions : `*` ne traverse pas les
#   dossiers, `**` si. Ils passent à git avec la magie `:(glob)`, qui suit la même règle. Seuls
#   `*`, et `**` employé comme segment entier (`src/**`, `**/package.json`), ont le même sens
#   des deux côtés : `?`, `+` et `!` n'ont pas celui des filtres GitHub, et `**` collé à un nom
#   (`**.js`) n'est pas reconnu par git. Aucun motif actuel ne les emploie.
# - Le dépôt doit être extrait sur le commit de fusion de la PR, ce que fait actions/checkout
#   sur `pull_request`, avec `fetch-depth: 2` pour avoir ses deux parents. Le premier est la
#   base de la PR, le second sa tête, comparée à PR_HEAD_SHA.
# - Écrit `<sortie>=true` ou `<sortie>=false` dans $GITHUB_OUTPUT. Sur toute erreur, le script
#   échoue sans rien écrire : le job `changes` échoue, et les jobs vérifiés tournent.

set -euo pipefail

if [ $# -lt 2 ]; then
  echo "::error::usage : $0 <sortie> <motif>..." >&2
  exit 2
fi
output=$1
shift

: "${GITHUB_OUTPUT:?GITHUB_OUTPUT non défini}"
: "${PR_HEAD_SHA:?PR_HEAD_SHA non défini}"

# Le commit extrait doit être la fusion de la tête de la PR dans sa base.
if ! head_parent=$(git rev-parse --verify --quiet 'HEAD^2'); then
  echo "::error::HEAD n'est pas un commit de fusion : base de la PR inconnue" >&2
  exit 1
fi
if [ "$head_parent" != "$PR_HEAD_SHA" ]; then
  echo "::error::le second parent de HEAD ($head_parent) n'est pas la tête de la PR ($PR_HEAD_SHA)" >&2
  exit 1
fi

pathspecs=()
for pattern in "$@"; do
  pathspecs+=(":(glob)$pattern")
done

# --no-renames : un fichier renommé compte sous son ancien et son nouveau chemin.
changed=$(git diff --name-only --no-renames 'HEAD^1' HEAD -- "${pathspecs[@]}")

if [ -n "$changed" ]; then
  echo "Fichiers modifiés qui correspondent aux motifs :"
  # Préfixe « - » : un chemin qui commence par `::` ne doit pas être lu comme une commande
  # de workflow par le runner.
  sed 's/^/- /' <<<"$changed"
  echo "$output=true" >>"$GITHUB_OUTPUT"
else
  echo "Aucun fichier modifié ne correspond aux motifs : les jobs qui en dépendent sont ignorés."
  echo "$output=false" >>"$GITHUB_OUTPUT"
fi
